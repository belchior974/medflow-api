namespace MedFlow.Domain.Model;

public sealed class Profissional
{
    public long Id { get; set; }
    public required string Nome { get; init; }
    public required string Especialidade { get; init; }
    public required string Crm { get; init; }
    public bool Ativo { get; init; } = true;
}
