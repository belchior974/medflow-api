using MedFlow.Domain.Model;
using MedFlow.Domain.Model.Enums;

namespace MedFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// LINHAS DE PERSISTENCIA - o mapeamento explicito entre tabela e dominio.
/// <para>
/// Diferenca deliberada em relacao a versao Spring: la, as entidades de dominio
/// carregavam anotacoes de mapeamento (<c>@Table</c>, <c>@Column</c>, <c>@Entity</c>) e
/// a conversao de enums dependia do comportamento implicito do driver. Aqui o dominio e
/// 100% limpo e a traducao vive nestes records, visivel e testavel. O enum vira string
/// em um lugar so, sem magica e sem diferenca de comportamento entre PostgreSQL e SQLite.
/// </para>
/// </summary>
internal sealed record ConsultaRow(
    long Id,
    long PacienteId,
    long ProfissionalId,
    DateTime DataHora,
    string Status,
    string Procedimento,
    string? CoberturaConvenio,
    DateTime CriadoEm)
{
    public Consulta ParaDominio() => Consulta.Reidratar(
        Id,
        PacienteId,
        ProfissionalId,
        DataHora,
        Procedimento,
        CriadoEm,
        Enum.Parse<StatusConsulta>(Status),
        CoberturaConvenio is null ? null : Enum.Parse<StatusCobertura>(CoberturaConvenio));
}

internal sealed record PacienteRow(
    long Id,
    string Nome,
    string Cpf,
    DateTime DataNascimento,
    string? Email,
    string? Telefone,
    long? ConvenioId)
{
    public Paciente ParaDominio() => new()
    {
        Id = Id,
        Nome = Nome,
        Cpf = Cpf,
        DataNascimento = DateOnly.FromDateTime(DataNascimento),
        Email = Email,
        Telefone = Telefone,
        ConvenioId = ConvenioId
    };
}

internal sealed record ProfissionalRow(
    long Id,
    string Nome,
    string Especialidade,
    string Crm,
    bool Ativo)
{
    public Profissional ParaDominio() => new()
    {
        Id = Id,
        Nome = Nome,
        Especialidade = Especialidade,
        Crm = Crm,
        Ativo = Ativo
    };
}
