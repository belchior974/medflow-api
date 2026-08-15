using System.Diagnostics;
using MedFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api;

/// <summary>
/// Tratamento de erro centralizado (SRP: nenhum endpoint trata excecao por conta).
/// <para>
/// Usa <see cref="IExceptionHandler"/> (.NET 8+) e devolve <c>ProblemDetails</c>
/// (RFC 7807). O <c>traceId</c> vai no corpo: com ele, o suporte pula direto para o trace
/// correspondente no Zipkin - a ponte pratica entre os tres pilares da observabilidade.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = MapearStatus(excecao);
        var caminho = contexto.Request.Path.Value ?? "/";

        if (status >= 500)
        {
            logger.LogError(excecao, "Erro inesperado em {Caminho}", caminho);
        }
        else
        {
            logger.LogDebug("Requisicao rejeitada em {Caminho}: {Mensagem}", caminho, excecao.Message);
        }

        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = excecao.Message,
            Instance = caminho,
            Extensions =
            {
                ["traceId"] = Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier,
                ["em"] = DateTime.Now
            }
        };

        if (excecao is RegraDeAgendamentoVioladaException regra)
        {
            problema.Extensions["regraViolada"] = regra.Regra;
        }

        contexto.Response.StatusCode = status;
        await contexto.Response.WriteAsJsonAsync(problema, ct);
        return true;
    }

    private static (int Status, string Titulo) MapearStatus(Exception excecao) => excecao switch
    {
        RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso nao encontrado"),
        RegraDeAgendamentoVioladaException => (StatusCodes.Status422UnprocessableEntity, "Regra de agendamento violada"),
        TransicaoDeStatusInvalidaException => (StatusCodes.Status422UnprocessableEntity, "Transicao de status invalida"),
        CoberturaNegadaException => (StatusCodes.Status422UnprocessableEntity, "Cobertura negada"),
        ArgumentException or FormatException => (StatusCodes.Status400BadRequest, "Requisicao invalida"),
        // 499 (Client Closed Request) nao existe em StatusCodes: e uma extensao do nginx.
        OperationCanceledException => (499, "Requisicao cancelada pelo cliente"),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno")
    };
}
