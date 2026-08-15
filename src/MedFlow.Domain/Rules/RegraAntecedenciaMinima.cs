using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;

namespace MedFlow.Domain.Rules;

/// <summary>
/// Nao permite agendar no passado nem com antecedencia menor que a minima.
/// <para>
/// Recebe <see cref="TimeProvider"/> (BCL, .NET 8+) em vez de usar <c>DateTime.Now</c>:
/// o tempo vira uma dependencia injetada e o teste consegue congelar o relogio.
/// </para>
/// </summary>
public sealed class RegraAntecedenciaMinima(ParametrosAgenda parametros, TimeProvider tempo)
    : IRegraAgendamento
{
    public string Nome => "ANTECEDENCIA_MINIMA";
    public int Ordem => 20;

    public Task ValidarAsync(Consulta consulta, CancellationToken ct = default)
    {
        var agora = tempo.GetLocalNow().DateTime;
        var limite = agora.AddHours(parametros.AntecedenciaMinimaHoras);

        if (consulta.DataHora < limite)
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome,
                $"A consulta deve ser agendada com no minimo {parametros.AntecedenciaMinimaHoras} "
                + "hora(s) de antecedencia.");
        }

        return Task.CompletedTask;
    }
}
