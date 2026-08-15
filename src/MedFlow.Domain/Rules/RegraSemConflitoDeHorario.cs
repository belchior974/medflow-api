using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Domain.Rules;

/// <summary>Regra mais cara (bate no banco): fica por ultimo na ordem de execucao.</summary>
public sealed class RegraSemConflitoDeHorario(IConsultaRepository repositorio) : IRegraAgendamento
{
    public string Nome => "SEM_CONFLITO_DE_HORARIO";
    public int Ordem => 40;

    public async Task ValidarAsync(Consulta consulta, CancellationToken ct = default)
    {
        var conflito = await repositorio.ExisteConflitoAsync(
            consulta.ProfissionalId, consulta.DataHora, ct);

        if (conflito)
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome, "Ja existe uma consulta neste horario para o profissional.");
        }
    }
}
