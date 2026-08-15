namespace MedFlow.Domain.Exceptions;

public sealed class RegraDeAgendamentoVioladaException(string regra, string motivo)
    : DomainException(motivo)
{
    public string Regra { get; } = regra;
}
