using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;
using MedFlow.Infrastructure.Persistence.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Persistence.EfCore;

/// <summary>ADAPTER EF Core do prontuario. Sem cache: isso e responsabilidade do decorador.</summary>
internal sealed class RegistroClinicoRepository(MedFlowDbContext contexto) : IRegistroClinicoRepository
{
    public async Task<RegistroClinico> SalvarAsync(RegistroClinico registro, CancellationToken ct = default)
    {
        contexto.RegistrosClinicos.Add(registro);
        await contexto.SaveChangesAsync(ct);
        return registro;
    }

    public Task<RegistroClinico?> BuscarPorIdAsync(long id, CancellationToken ct = default) =>
        contexto.RegistrosClinicos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<RegistroClinico>> BuscarPorFiltroAsync(
        FiltroHistoricoClinico filtro, CancellationToken ct = default) =>
        await contexto.RegistrosClinicos
            .AsNoTracking()
            .AplicarFiltro(filtro)
            .ToListAsync(ct);

    /// <summary>
    /// PROJECAO: o <c>Select</c> antes do <c>ToListAsync</c> faz o provider gerar
    /// <c>SELECT id, consulta_id, diagnostico, data_registro</c> - a coluna
    /// <c>prescricao</c> (varchar 1000) nunca sai do banco.
    /// </summary>
    public async Task<IReadOnlyList<RegistroClinicoResumo>> ResumoPorPacienteAsync(
        long pacienteId, CancellationToken ct = default) =>
        await contexto.RegistrosClinicos
            .AsNoTracking()
            .Where(r => r.PacienteId == pacienteId)
            .OrderByDescending(r => r.DataRegistro)
            .Select(r => new RegistroClinicoResumo(r.Id, r.ConsultaId, r.Diagnostico, r.DataRegistro))
            .ToListAsync(ct);
}
