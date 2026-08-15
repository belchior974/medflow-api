using MedFlow.Domain.Model;

namespace MedFlow.Domain.Repositories;

/// <summary>
/// PORTA de saida para persistencia de consultas (DIP).
/// <para>
/// A camada de aplicacao depende desta abstracao, nunca do Dapper. A implementacao
/// concreta (<c>ConsultaDapperRepository</c>) vive em MedFlow.Infrastructure - projeto
/// que MedFlow.Domain nao referencia, e por isso nem consegue enxergar.
/// </para>
/// <para>ISP: porta pequena, so com o que os use cases de agendamento precisam.</para>
/// </summary>
public interface IConsultaRepository
{
    Task<Consulta> SalvarAsync(Consulta consulta, CancellationToken ct = default);

    Task<Consulta?> BuscarPorIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<Consulta>> BuscarPorProfissionalEIntervaloAsync(
        long profissionalId, DateTime inicio, DateTime fim, CancellationToken ct = default);

    Task<bool> ExisteConflitoAsync(long profissionalId, DateTime dataHora, CancellationToken ct = default);

    Task<IReadOnlyList<Consulta>> BuscarPorPacienteAsync(long pacienteId, CancellationToken ct = default);
}
