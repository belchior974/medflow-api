namespace MedFlow.Domain.Model;

public sealed class Paciente
{
    public long Id { get; set; }
    public required string Nome { get; init; }
    public required string Cpf { get; init; }
    public required DateOnly DataNascimento { get; init; }
    public string? Email { get; init; }
    public string? Telefone { get; init; }
    public long? ConvenioId { get; init; }

    /// <summary>Regra de dominio: paciente particular nao passa pela validacao de convenio.</summary>
    public bool PossuiConvenio => ConvenioId is not null;
}
