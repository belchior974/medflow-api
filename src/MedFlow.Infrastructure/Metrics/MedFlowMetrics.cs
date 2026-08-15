using System.Diagnostics.Metrics;
using MedFlow.Application.Ports;
using MedFlow.Domain.Model.Enums;

namespace MedFlow.Infrastructure.Metrics;

/// <summary>
/// ADAPTER de metricas: traduz eventos de negocio em instrumentos do
/// <c>System.Diagnostics.Metrics</c>, exportados ao Prometheus pelo OpenTelemetry.
/// <para>Metricas de negocio publicadas (nomes ja no formato Prometheus):</para>
/// <list type="bullet">
///   <item><c>medflow_consultas_criadas_total{cobertura=...}</c></item>
///   <item><c>medflow_consultas_canceladas_total</c> / <c>..._confirmadas_total</c></item>
///   <item><c>medflow_convenio_fallback_acionado_total</c> &lt;- prova o fallback no video</item>
///   <item><c>medflow_cache_hit_total{cache=...,camada=L1|L2}</c> / <c>medflow_cache_miss_total</c></item>
///   <item><c>medflow_agendamento_regra_violada_total{regra=...}</c></item>
/// </list>
/// </summary>
public sealed class MedFlowMetrics : IMetricasPort, IDisposable
{
    /// <summary>Nome registrado no OpenTelemetry (ver Program.cs).</summary>
    public const string NomeMeter = "MedFlow";

    private readonly Meter _meter;
    private readonly Counter<long> _consultasCriadas;
    private readonly Counter<long> _consultasCanceladas;
    private readonly Counter<long> _consultasConfirmadas;
    private readonly Counter<long> _fallbackConvenio;
    private readonly Counter<long> _coberturaVerificada;
    private readonly Counter<long> _cacheHit;
    private readonly Counter<long> _cacheMiss;
    private readonly Counter<long> _regraViolada;

    public MedFlowMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(NomeMeter);

        _consultasCriadas = _meter.CreateCounter<long>(
            "medflow.consultas.criadas", description: "Consultas agendadas com sucesso");
        _consultasCanceladas = _meter.CreateCounter<long>("medflow.consultas.canceladas");
        _consultasConfirmadas = _meter.CreateCounter<long>("medflow.consultas.confirmadas");
        _fallbackConvenio = _meter.CreateCounter<long>(
            "medflow.convenio.fallback.acionado",
            description: "Vezes em que o fallback do servico de convenio foi acionado");
        _coberturaVerificada = _meter.CreateCounter<long>("medflow.convenio.verificacoes");
        _cacheHit = _meter.CreateCounter<long>("medflow.cache.hit");
        _cacheMiss = _meter.CreateCounter<long>("medflow.cache.miss");
        _regraViolada = _meter.CreateCounter<long>("medflow.agendamento.regra.violada");
    }

    public void ConsultaCriada(StatusCobertura? cobertura) =>
        _consultasCriadas.Add(1, new KeyValuePair<string, object?>(
            "cobertura", (cobertura ?? StatusCobertura.NaoAplicavel).ToString()));

    public void ConsultaCancelada() => _consultasCanceladas.Add(1);

    public void ConsultaConfirmada() => _consultasConfirmadas.Add(1);

    public void FallbackConvenioAcionado() => _fallbackConvenio.Add(1);

    public void CoberturaVerificada(StatusCobertura resultado) =>
        _coberturaVerificada.Add(1, new KeyValuePair<string, object?>("resultado", resultado.ToString()));

    public void CacheHit(string cache, string camada) =>
        _cacheHit.Add(1,
            new KeyValuePair<string, object?>("cache", cache),
            new KeyValuePair<string, object?>("camada", camada));

    public void CacheMiss(string cache) =>
        _cacheMiss.Add(1, new KeyValuePair<string, object?>("cache", cache));

    public void RegraDeAgendamentoViolada(string regra) =>
        _regraViolada.Add(1, new KeyValuePair<string, object?>("regra", regra));

    public void Dispose() => _meter.Dispose();
}
