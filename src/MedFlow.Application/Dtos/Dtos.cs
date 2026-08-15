using System.ComponentModel.DataAnnotations;
using MedFlow.Domain.Model;
using MedFlow.Domain.Model.Enums;

namespace MedFlow.Application.Dtos;

// ---------------------------------------------------------------------------
// Pacientes
// ---------------------------------------------------------------------------

public sealed record PacienteRequest(
    [property: Required(ErrorMessage = "nome e obrigatorio")]
    [property: StringLength(150, MinimumLength = 2)] string Nome,

    [property: Required(ErrorMessage = "cpf e obrigatorio")]
    [property: StringLength(14, MinimumLength = 11)] string Cpf,

    [property: Required(ErrorMessage = "dataNascimento e obrigatoria")] DateOnly DataNascimento,

    [property: EmailAddress(ErrorMessage = "email invalido")] string? Email = null,
    string? Telefone = null,
    long? ConvenioId = null);

public sealed record PacienteResponse(
    long Id, string Nome, string Cpf, DateOnly DataNascimento,
    string? Email, string? Telefone, long? ConvenioId)
{
    public static PacienteResponse De(Paciente p) =>
        new(p.Id, p.Nome, p.Cpf, p.DataNascimento, p.Email, p.Telefone, p.ConvenioId);
}

// ---------------------------------------------------------------------------
// Profissionais
// ---------------------------------------------------------------------------

public sealed record ProfissionalRequest(
    [property: Required(ErrorMessage = "nome e obrigatorio")] string Nome,
    [property: Required(ErrorMessage = "especialidade e obrigatoria")] string Especialidade,
    [property: Required(ErrorMessage = "crm e obrigatorio")] string Crm);

public sealed record ProfissionalResponse(
    long Id, string Nome, string Especialidade, string Crm, bool Ativo)
{
    public static ProfissionalResponse De(Profissional p) =>
        new(p.Id, p.Nome, p.Especialidade, p.Crm, p.Ativo);
}

// ---------------------------------------------------------------------------
// Consultas
// ---------------------------------------------------------------------------

public sealed record AgendarConsultaRequest(
    [property: Range(1, long.MaxValue, ErrorMessage = "pacienteId e obrigatorio")] long PacienteId,
    [property: Range(1, long.MaxValue, ErrorMessage = "profissionalId e obrigatorio")] long ProfissionalId,
    [property: Required(ErrorMessage = "dataHora e obrigatoria")] DateTime DataHora,
    [property: Required(ErrorMessage = "procedimento e obrigatorio")]
    [property: StringLength(120)] string Procedimento);

public sealed record ConsultaResponse(
    long Id,
    long PacienteId,
    long ProfissionalId,
    DateTime DataHora,
    StatusConsulta Status,
    string Procedimento,
    StatusCobertura? CoberturaConvenio,
    DateTime CriadoEm)
{
    public static ConsultaResponse De(Consulta c) =>
        new(c.Id, c.PacienteId, c.ProfissionalId, c.DataHora, c.Status,
            c.Procedimento, c.CoberturaConvenio, c.CriadoEm);
}

// ---------------------------------------------------------------------------
// Disponibilidade
// ---------------------------------------------------------------------------

/// <summary>Snapshot cacheavel da agenda de um profissional em um dia.</summary>
public sealed record DisponibilidadeSnapshot(
    long ProfissionalId,
    DateOnly Data,
    IReadOnlyList<string> HorariosLivres,
    int TotalSlots);

/// <summary>Evento empurrado via Server-Sent Events quando um horario e liberado/ocupado.</summary>
public sealed record DisponibilidadeEvent(
    long ProfissionalId,
    DateOnly Data,
    string? Horario,
    string Tipo,
    DateTime Em)
{
    public static DisponibilidadeEvent Liberado(long profissionalId, DateTime dataHora) =>
        new(profissionalId, DateOnly.FromDateTime(dataHora),
            dataHora.ToString("HH:mm"), "LIBERADO", DateTime.Now);

    public static DisponibilidadeEvent Ocupado(long profissionalId, DateTime dataHora) =>
        new(profissionalId, DateOnly.FromDateTime(dataHora),
            dataHora.ToString("HH:mm"), "OCUPADO", DateTime.Now);

    public static DisponibilidadeEvent Heartbeat(long profissionalId) =>
        new(profissionalId, DateOnly.FromDateTime(DateTime.Now), null, "HEARTBEAT", DateTime.Now);
}

// ---------------------------------------------------------------------------
// Prontuario
// ---------------------------------------------------------------------------

public sealed record RegistrarAtendimentoRequest(
    [property: Range(1, long.MaxValue, ErrorMessage = "consultaId e obrigatorio")] long ConsultaId,
    [property: Required(ErrorMessage = "diagnostico e obrigatorio")]
    [property: StringLength(500)] string Diagnostico,
    [property: StringLength(1000)] string? Prescricao = null);

public sealed record RegistroClinicoResponse(
    long Id, long ConsultaId, long PacienteId, long ProfissionalId,
    string Diagnostico, string? Prescricao, DateTime DataRegistro)
{
    public static RegistroClinicoResponse De(RegistroClinico r) =>
        new(r.Id, r.ConsultaId, r.PacienteId, r.ProfissionalId,
            r.Diagnostico, r.Prescricao, r.DataRegistro);
}
