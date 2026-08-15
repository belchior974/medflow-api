using MedFlow.Domain.Model.Enums;

namespace MedFlow.Application.Ports;

/// <summary>
/// PORTA de saida para metricas de negocio (DIP).
/// <para>
/// Os use cases nao importam <c>System.Diagnostics.Metrics</c> nem OpenTelemetry: eles
/// falam a linguagem do negocio ("uma consulta foi criada"). O adapter
/// <c>MedFlowMetrics</c> traduz isso para instrumentos exportados ao Prometheus.
/// </para>
/// </summary>
public interface IMetricasPort
{
    void ConsultaCriada(StatusCobertura? cobertura);
    void ConsultaCancelada();
    void ConsultaConfirmada();
    void FallbackConvenioAcionado();
    void CoberturaVerificada(StatusCobertura resultado);
    void CacheHit(string cache, string camada);
    void CacheMiss(string cache);
    void RegraDeAgendamentoViolada(string regra);
}
