namespace MedFlow.Domain.Model.Enums;

/// <summary>
/// Ciclo de vida da consulta.
/// <para>
/// <see cref="AguardandoValidacaoConvenio"/> e o estado de <b>degradacao graciosa</b>:
/// quando o servico externo de convenio esta indisponivel (circuito aberto), a consulta
/// e criada mesmo assim neste status, em vez de o agendamento falhar.
/// </para>
/// </summary>
public enum StatusConsulta
{
    Agendada,
    AguardandoValidacaoConvenio,
    Confirmada,
    Cancelada,
    Realizada
}

public static class StatusConsultaExtensions
{
    public static bool OcupaAgenda(this StatusConsulta status) =>
        status is not StatusConsulta.Cancelada;

    public static bool PodeSerCancelada(this StatusConsulta status) =>
        status is StatusConsulta.Agendada
               or StatusConsulta.AguardandoValidacaoConvenio
               or StatusConsulta.Confirmada;

    public static bool PodeSerConfirmada(this StatusConsulta status) =>
        status is StatusConsulta.Agendada or StatusConsulta.AguardandoValidacaoConvenio;
}
