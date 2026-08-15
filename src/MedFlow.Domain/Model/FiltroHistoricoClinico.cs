namespace MedFlow.Domain.Model;

/// <summary>
/// Objeto de consulta do dominio, traduzido para uma composicao de <c>IQueryable</c>
/// no repositorio de infraestrutura. Todos os campos sao opcionais (nulos sao ignorados
/// na composicao dos predicados) - e o que torna a busca "dinamica".
/// </summary>
public sealed record FiltroHistoricoClinico(
    long? PacienteId = null,
    long? ProfissionalId = null,
    DateTime? PeriodoInicio = null,
    DateTime? PeriodoFim = null,
    string? Diagnostico = null)
{
    public bool Vazio =>
        PacienteId is null && ProfissionalId is null &&
        PeriodoInicio is null && PeriodoFim is null &&
        string.IsNullOrWhiteSpace(Diagnostico);
}
