using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Domain.Rules;

/// <summary>O profissional precisa existir e estar ativo.</summary>
public sealed class RegraProfissionalDisponivel(IProfissionalRepository repositorio) : IRegraAgendamento
{
    public string Nome => "PROFISSIONAL_DISPONIVEL";
    public int Ordem => 30;

    public async Task ValidarAsync(Consulta consulta, CancellationToken ct = default)
    {
        var profissional = await repositorio.BuscarPorIdAsync(consulta.ProfissionalId, ct)
            ?? throw new RecursoNaoEncontradoException("Profissional", consulta.ProfissionalId);

        if (!profissional.Ativo)
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome, $"Profissional {profissional.Nome} esta inativo.");
        }
    }
}
