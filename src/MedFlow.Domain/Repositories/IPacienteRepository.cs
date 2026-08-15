using MedFlow.Domain.Model;

namespace MedFlow.Domain.Repositories;

public interface IPacienteRepository
{
    Task<Paciente> SalvarAsync(Paciente paciente, CancellationToken ct = default);
    Task<Paciente?> BuscarPorIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Paciente>> ListarTodosAsync(CancellationToken ct = default);
}
