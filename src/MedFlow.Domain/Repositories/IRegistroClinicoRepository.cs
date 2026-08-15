using MedFlow.Domain.Model;

namespace MedFlow.Domain.Repositories;

/// <summary>PORTA de saida para o prontuario (DIP).</summary>
public interface IRegistroClinicoRepository
{
    Task<RegistroClinico> SalvarAsync(RegistroClinico registro, CancellationToken ct = default);

    Task<RegistroClinico?> BuscarPorIdAsync(long id, CancellationToken ct = default);

    /// <summary>Busca dinamica, traduzida para composicao de IQueryable no repositorio.</summary>
    Task<IReadOnlyList<RegistroClinico>> BuscarPorFiltroAsync(
        FiltroHistoricoClinico filtro, CancellationToken ct = default);

    /// <summary>Projecao: carrega apenas as colunas do resumo, evitando overfetching.</summary>
    Task<IReadOnlyList<RegistroClinicoResumo>> ResumoPorPacienteAsync(
        long pacienteId, CancellationToken ct = default);
}
