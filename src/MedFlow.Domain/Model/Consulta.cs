using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model.Enums;

namespace MedFlow.Domain.Model;

/// <summary>
/// Agregado central do dominio.
/// <para>
/// SRP: a entidade e dona das suas <b>proprias transicoes de estado</b>
/// (<see cref="Cancelar"/>, <see cref="Confirmar"/>, <see cref="AplicarCobertura"/>).
/// Os use cases orquestram, mas nao decidem se a transicao e valida - isso pertence
/// ao dominio.
/// </para>
/// <para>
/// Note que esta classe nao tem UM UNICO atributo de mapeamento
/// (<c>[Table]</c>, <c>[Column]</c>, <c>[Key]</c>). O mapeamento vive inteiramente na
/// infraestrutura: linhas de persistencia explicitas no Dapper e
/// <c>IEntityTypeConfiguration</c> no EF Core.
/// </para>
/// </summary>
public sealed class Consulta
{
    public long Id { get; set; }
    public required long PacienteId { get; init; }
    public required long ProfissionalId { get; init; }
    public required DateTime DataHora { get; init; }
    public required string Procedimento { get; init; }
    public required DateTime CriadoEm { get; init; }

    public StatusConsulta Status { get; private set; } = StatusConsulta.Agendada;
    public StatusCobertura? CoberturaConvenio { get; private set; }

    /// <summary>Reidrata a entidade a partir do banco sem passar pelas regras de transicao.</summary>
    public static Consulta Reidratar(
        long id,
        long pacienteId,
        long profissionalId,
        DateTime dataHora,
        string procedimento,
        DateTime criadoEm,
        StatusConsulta status,
        StatusCobertura? coberturaConvenio) => new()
        {
            Id = id,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            DataHora = dataHora,
            Procedimento = procedimento,
            CriadoEm = criadoEm,
            Status = status,
            CoberturaConvenio = coberturaConvenio
        };

    public Consulta Cancelar()
    {
        if (!Status.PodeSerCancelada())
        {
            throw new TransicaoDeStatusInvalidaException(Id, Status, StatusConsulta.Cancelada);
        }

        Status = StatusConsulta.Cancelada;
        return this;
    }

    public Consulta Confirmar()
    {
        if (!Status.PodeSerConfirmada())
        {
            throw new TransicaoDeStatusInvalidaException(Id, Status, StatusConsulta.Confirmada);
        }

        Status = StatusConsulta.Confirmada;
        return this;
    }

    /// <summary>
    /// Aplica o resultado da verificacao de convenio.
    /// <para>
    /// Este e o ponto exato da <b>degradacao graciosa</b>: cobertura
    /// <see cref="StatusCobertura.Indeterminada"/> (servico externo fora do ar) nao derruba
    /// o agendamento - a consulta apenas nasce aguardando validacao.
    /// </para>
    /// </summary>
    public Consulta AplicarCobertura(StatusCobertura cobertura)
    {
        CoberturaConvenio = cobertura;
        Status = cobertura is StatusCobertura.Indeterminada
            ? StatusConsulta.AguardandoValidacaoConvenio
            : StatusConsulta.Agendada;
        return this;
    }
}
