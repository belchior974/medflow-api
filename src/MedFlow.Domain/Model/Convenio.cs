namespace MedFlow.Domain.Model;

public sealed class Convenio
{
    public long Id { get; set; }
    public required string Nome { get; init; }
    public bool Ativo { get; init; } = true;
}
