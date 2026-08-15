namespace MedFlow.Domain.Rules;

/// <summary>
/// Parametros da agenda como um tipo do <b>dominio</b>. A infraestrutura apenas fornece
/// a instancia a partir do appsettings.json, de modo que as regras de negocio nao
/// dependem de <c>IOptions&lt;T&gt;</c> nem de qualquer tipo de configuracao.
/// </summary>
public sealed record ParametrosAgenda(
    int HoraInicio = 8,
    int HoraFim = 18,
    int DuracaoConsultaMinutos = 30,
    int AntecedenciaMinimaHoras = 1)
{
    /// <summary>Todos os slots teoricos de um dia (a disponibilidade real subtrai os ocupados).</summary>
    public IReadOnlyList<DateTime> SlotsDoDia(DateOnly data)
    {
        var slots = new List<DateTime>();
        var cursor = data.ToDateTime(new TimeOnly(HoraInicio, 0));
        var fim = data.ToDateTime(new TimeOnly(HoraFim, 0));

        while (cursor < fim)
        {
            slots.Add(cursor);
            cursor = cursor.AddMinutes(DuracaoConsultaMinutos);
        }

        return slots;
    }

    public bool DentroDoHorarioDeAtendimento(DateTime dataHora) =>
        dataHora.Hour >= HoraInicio && dataHora.Hour < HoraFim;

    public bool AlinhadoAoSlot(DateTime dataHora) =>
        dataHora.Second == 0 && dataHora.Millisecond == 0
        && dataHora.Minute % DuracaoConsultaMinutos == 0;
}
