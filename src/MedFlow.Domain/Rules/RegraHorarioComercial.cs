using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;

namespace MedFlow.Domain.Rules;

/// <summary>Consulta so pode cair em dia util, dentro do horario e alinhada ao slot.</summary>
public sealed class RegraHorarioComercial(ParametrosAgenda parametros) : IRegraAgendamento
{
    public string Nome => "HORARIO_COMERCIAL";
    public int Ordem => 10;

    public Task ValidarAsync(Consulta consulta, CancellationToken ct = default)
    {
        var dataHora = consulta.DataHora;

        if (dataHora.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome, $"A clinica nao atende aos fins de semana ({dataHora.DayOfWeek}).");
        }

        if (!parametros.DentroDoHorarioDeAtendimento(dataHora))
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome, $"Horario fora do expediente ({parametros.HoraInicio}h as {parametros.HoraFim}h).");
        }

        if (!parametros.AlinhadoAoSlot(dataHora))
        {
            throw new RegraDeAgendamentoVioladaException(
                Nome,
                $"Agendamento deve iniciar em um slot de {parametros.DuracaoConsultaMinutos} minutos "
                + "(ex.: 09:00, 09:30).");
        }

        return Task.CompletedTask;
    }
}
