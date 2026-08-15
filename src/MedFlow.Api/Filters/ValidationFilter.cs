using System.ComponentModel.DataAnnotations;

namespace MedFlow.Api.Filters;

/// <summary>
/// Valida o corpo da requisicao com DataAnnotations antes de o endpoint rodar.
/// <para>
/// Equivalente do <c>RequestValidator</c> da versao Spring: no roteamento funcional
/// tambem nao existe <c>@Valid</c> automatico. Centralizar aqui evita repetir o bloco em
/// cada endpoint (SRP - o endpoint cuida do fluxo HTTP, este filtro cuida da validacao).
/// </para>
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var alvo = context.Arguments.OfType<T>().FirstOrDefault();
        if (alvo is null)
        {
            return Results.Problem("Corpo da requisicao ausente ou invalido.", statusCode: 400);
        }

        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(alvo);

        if (Validator.TryValidateObject(alvo, contexto, resultados, validateAllProperties: true))
        {
            return await next(context);
        }

        var erros = resultados
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty),
                        (r, campo) => new { Campo = campo, Erro = r.ErrorMessage ?? "invalido" })
            .GroupBy(x => x.Campo)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Erro).ToArray());

        return Results.ValidationProblem(erros);
    }
}

public static class ValidationFilterExtensions
{
    /// <summary>Acucar sintatico: <c>.ComValidacao&lt;PacienteRequest&gt;()</c>.</summary>
    public static RouteHandlerBuilder ComValidacao<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
