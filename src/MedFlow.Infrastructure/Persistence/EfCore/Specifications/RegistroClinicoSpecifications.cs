using MedFlow.Domain.Model;

namespace MedFlow.Infrastructure.Persistence.EfCore.Specifications;

/// <summary>
/// Equivalente .NET da JPA <c>Specification</c>: cada criterio e um metodo de extensao
/// componivel sobre <c>IQueryable</c>.
/// <para>
/// Continua sendo <b>dinamico</b> (campos nulos simplesmente nao entram no WHERE) e
/// <b>tipado</b> (a expressao e verificada pelo compilador e traduzida para SQL pelo
/// provider - nao ha concatenacao de string, logo nao ha superficie para SQL injection).
/// Comparado a Criteria API do JPA, o codigo e drasticamente mais curto e legivel.
/// </para>
/// </summary>
internal static class RegistroClinicoSpecifications
{
    public static IQueryable<RegistroClinico> DoPaciente(
        this IQueryable<RegistroClinico> query, long? pacienteId) =>
        pacienteId is null ? query : query.Where(r => r.PacienteId == pacienteId.Value);

    public static IQueryable<RegistroClinico> DoProfissional(
        this IQueryable<RegistroClinico> query, long? profissionalId) =>
        profissionalId is null ? query : query.Where(r => r.ProfissionalId == profissionalId.Value);

    public static IQueryable<RegistroClinico> APartirDe(
        this IQueryable<RegistroClinico> query, DateTime? inicio) =>
        inicio is null ? query : query.Where(r => r.DataRegistro >= inicio.Value);

    public static IQueryable<RegistroClinico> Ate(
        this IQueryable<RegistroClinico> query, DateTime? fim) =>
        fim is null ? query : query.Where(r => r.DataRegistro <= fim.Value);

    public static IQueryable<RegistroClinico> DiagnosticoContendo(
        this IQueryable<RegistroClinico> query, string? termo) =>
        string.IsNullOrWhiteSpace(termo)
            ? query
            : query.Where(r => r.Diagnostico.ToLower().Contains(termo.ToLower()));

    /// <summary>Compoe todos os criterios nao nulos.</summary>
    public static IQueryable<RegistroClinico> AplicarFiltro(
        this IQueryable<RegistroClinico> query, FiltroHistoricoClinico filtro) =>
        query
            .DoPaciente(filtro.PacienteId)
            .DoProfissional(filtro.ProfissionalId)
            .APartirDe(filtro.PeriodoInicio)
            .Ate(filtro.PeriodoFim)
            .DiagnosticoContendo(filtro.Diagnostico)
            .OrderByDescending(r => r.DataRegistro);
}
