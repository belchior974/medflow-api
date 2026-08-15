using MedFlow.Domain.Model;

namespace MedFlow.Domain.Repositories;

public interface IProfissionalRepository
{
    Task<Profissional> SalvarAsync(Profissional profissional, CancellationToken ct = default);
    Task<Profissional?> BuscarPorIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Profissional>> ListarTodosAsync(CancellationToken ct = default);
}
