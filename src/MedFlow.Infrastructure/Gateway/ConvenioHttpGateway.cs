using System.Net;
using System.Net.Http.Json;
using MedFlow.Application.Ports;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Gateways;
using MedFlow.Domain.Model.Enums;
using MedFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace MedFlow.Infrastructure.Gateway;

/// <summary>
/// ADAPTER do servico EXTERNO de convenio - o ponto critico protegido pelos padroes de
/// resiliencia do Modulo 4, aqui implementados com <b>Polly v8</b>.
/// <para>Pilha de protecao (de fora para dentro), identica a da versao Resilience4j:</para>
/// <code>
///   Retry  -&gt;  CircuitBreaker  -&gt;  Timeout  -&gt;  ConcurrencyLimiter  -&gt;  HttpClient
/// </code>
/// <para>
/// <b>A distincao que importa</b>: falha de <i>negocio</i> (o convenio respondeu e negou,
/// HTTP 4xx) nao e falha <i>tecnica</i>. <see cref="CoberturaNegadaException"/> nao esta
/// em nenhum <c>ShouldHandle</c> do pipeline - ela atravessa retry e circuit breaker sem
/// ser contabilizada (o equivalente exato do <c>ignore-exceptions</c> do Resilience4j) e
/// e convertida em <see cref="StatusCobertura.NaoCoberto"/> no catch especifico.
/// </para>
/// <para>
/// Falha tecnica (timeout, 5xx, conexao recusada, circuito aberto) cai no catch generico
/// e vira <see cref="StatusCobertura.Indeterminada"/>: a consulta e agendada mesmo assim,
/// com status AguardandoValidacaoConvenio. <b>Degradacao graciosa</b> - a API nunca
/// devolve erro por causa do convenio.
/// </para>
/// </summary>
public sealed class ConvenioHttpGateway(
    HttpClient http,
    ResiliencePipelineProvider<string> pipelines,
    IMetricasPort metricas,
    ILogger<ConvenioHttpGateway> logger) : IConvenioGateway
{
    public const string NomePipeline = "convenio-service";

    public async Task<StatusCobertura> VerificarCoberturaAsync(
        long pacienteId, long convenioId, string procedimento, CancellationToken ct = default)
    {
        var pipeline = pipelines.GetPipeline<StatusCobertura>(NomePipeline);

        try
        {
            return await pipeline.ExecuteAsync(
                async token => await ChamarConvenioAsync(pacienteId, convenioId, procedimento, token),
                ct);
        }
        catch (CoberturaNegadaException e)
        {
            // NEGATIVA DE NEGOCIO: o convenio respondeu. Nao conta como falha para o
            // circuito e nao incrementa a metrica de fallback tecnico.
            logger.LogInformation(
                "Cobertura negada pelo convenio {ConvenioId} (regra de negocio): {Motivo}",
                convenioId, e.Message);
            return StatusCobertura.NaoCoberto;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // FALLBACK TECNICO: timeout, 5xx, conexao recusada ou circuito aberto.
            metricas.FallbackConvenioAcionado();
            logger.LogWarning(
                "FALLBACK acionado para o servico de convenio (paciente={PacienteId}, convenio={ConvenioId}): "
                + "{Tipo} - {Mensagem}. A consulta sera agendada como AguardandoValidacaoConvenio.",
                pacienteId, convenioId, e.GetType().Name, e.Message);

            return StatusCobertura.Indeterminada;
        }
    }

    private async Task<StatusCobertura> ChamarConvenioAsync(
        long pacienteId, long convenioId, string procedimento, CancellationToken ct)
    {
        var url = $"/convenios/{convenioId}/cobertura"
                + $"?pacienteId={pacienteId}&procedimento={Uri.EscapeDataString(procedimento)}";

        logger.LogDebug("Verificando cobertura: paciente={PacienteId} convenio={ConvenioId} proc={Proc}",
            pacienteId, convenioId, procedimento);

        using var resposta = await http.GetAsync(url, ct);

        // 4xx = o convenio RESPONDEU negando: regra de negocio, nao falha tecnica.
        if ((int)resposta.StatusCode is >= 400 and < 500)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            throw new CoberturaNegadaException(
                $"Convenio {convenioId} negou a cobertura de '{procedimento}': "
                + (string.IsNullOrWhiteSpace(corpo) ? "sem detalhes" : corpo));
        }

        // 5xx e demais falhas viram HttpRequestException -> tratadas pelo pipeline.
        resposta.EnsureSuccessStatusCode();

        var payload = await resposta.Content.ReadFromJsonAsync<RespostaCobertura>(ct);
        return payload?.Coberto == true ? StatusCobertura.Coberto : StatusCobertura.NaoCoberto;
    }

    /// <summary>Contrato de resposta do servico externo.</summary>
    private sealed record RespostaCobertura(bool Coberto, string? Motivo);
}

/// <summary>
/// Monta o pipeline do Polly a partir do appsettings.json.
/// <para>
/// Cada bloco tem correspondencia direta com a configuracao do Resilience4j na versao
/// Spring, o que facilita a comparacao lado a lado durante a apresentacao.
/// </para>
/// </summary>
public static class ConvenioPipelineFactory
{
    public static void Configurar(
        ResiliencePipelineBuilder<StatusCobertura> builder,
        MedFlowOptions.ConvenioOptions opcoes,
        CircuitBreakerStateProvider estadoDoCircuito)
    {
        // Nao trata CoberturaNegadaException: negativas de negocio passam direto.
        var falhasTecnicas = new PredicateBuilder<StatusCobertura>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutException>()
            .Handle<Polly.Timeout.TimeoutRejectedException>();

        if (opcoes.MaxTentativas < 1)
        {
            throw new InvalidOperationException(
                $"MedFlow:Convenio:MaxTentativas deve ser >= 1 (recebido: {opcoes.MaxTentativas}). " +
                "O valor e o TOTAL de tentativas, entao 1 significa uma chamada sem retentativa.");
        }

        // MaxTentativas e o TOTAL de tentativas; as retentativas sao uma a menos. Com
        // MaxTentativas = 1 nao ha retentativa alguma, e a estrategia simplesmente nao
        // entra no pipeline: o Polly v8 rejeita MaxRetryAttempts = 0 na validacao das
        // opcoes, o que derrubava o agendamento inteiro com HTTP 500.
        if (opcoes.MaxTentativas > 1)
        {
            // 1. RETRY (mais externo) - backoff exponencial, como no Resilience4j
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<StatusCobertura>
            {
                ShouldHandle = falhasTecnicas,
                MaxRetryAttempts = opcoes.MaxTentativas - 1,
                Delay = TimeSpan.FromMilliseconds(opcoes.DelayInicialMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });
        }

        builder
            // 2. CIRCUIT BREAKER
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<StatusCobertura>
            {
                ShouldHandle = falhasTecnicas,
                FailureRatio = opcoes.LimiarDeFalha,
                MinimumThroughput = opcoes.ChamadasMinimas,
                SamplingDuration = TimeSpan.FromSeconds(opcoes.JanelaAmostragemSegundos),
                BreakDuration = TimeSpan.FromSeconds(opcoes.DuracaoCircuitoAbertoSegundos),
                StateProvider = estadoDoCircuito
            })
            // 3. TIMEOUT por tentativa
            .AddTimeout(TimeSpan.FromMilliseconds(opcoes.TimeoutMs))
            // 4. BULKHEAD - limita a concorrencia contra a dependencia
            .AddConcurrencyLimiter(opcoes.MaxChamadasConcorrentes);
    }
}
