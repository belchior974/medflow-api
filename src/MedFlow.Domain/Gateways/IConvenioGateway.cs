using MedFlow.Domain.Model.Enums;

namespace MedFlow.Domain.Gateways;

/// <summary>
/// PORTA de saida para o servico EXTERNO de convenio (DIP).
/// <para>
/// O use case de agendamento nao sabe que existe HTTP, HttpClient, Polly ou circuit
/// breaker: ele so conhece este contrato. Toda a resiliencia esta na implementacao
/// (<c>ConvenioHttpGateway</c>), em MedFlow.Infrastructure.
/// </para>
/// <para>
/// <b>Contrato de resiliencia</b>: este metodo <b>nunca lanca erro tecnico</b>. Se o
/// servico externo estiver fora do ar, a implementacao devolve
/// <see cref="StatusCobertura.Indeterminada"/> pelo fallback.
/// </para>
/// </summary>
public interface IConvenioGateway
{
    Task<StatusCobertura> VerificarCoberturaAsync(
        long pacienteId, long convenioId, string procedimento, CancellationToken ct = default);
}
