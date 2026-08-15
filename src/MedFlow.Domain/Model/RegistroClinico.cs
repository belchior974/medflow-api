namespace MedFlow.Domain.Model;

/// <summary>
/// Prontuario eletronico - mapeado pelo EF Core.
/// <para>
/// Escolha arquitetural: o historico clinico exige consultas dinamicas e combinaveis
/// (paciente + periodo + profissional + diagnostico), cenario em que a composicao de
/// <c>IQueryable</c> do EF Core e superior ao SQL manual do Dapper. Ja o agendamento,
/// que e caminho quente e de forma fixa, usa Dapper.
/// </para>
/// </summary>
public sealed class RegistroClinico
{
    public long Id { get; set; }
    public required long ConsultaId { get; init; }
    public required long PacienteId { get; init; }
    public required long ProfissionalId { get; init; }
    public required string Diagnostico { get; init; }
    public string? Prescricao { get; init; }
    public required DateTime DataRegistro { get; init; }
}
