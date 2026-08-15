using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Gateways;
using MedFlow.Domain.Model;
using MedFlow.Domain.Model.Enums;
using MedFlow.Domain.Repositories;
using MedFlow.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace MedFlow.Application.UseCases;

/// <summary>
/// Use case central da API.
/// <para>
/// <b>SRP</b>: orquestra o fluxo (validar -&gt; verificar convenio -&gt; persistir -&gt;
/// invalidar cache -&gt; notificar), delegando cada responsabilidade. Nao valida regras
/// (delega a <see cref="IRegraAgendamento"/>), nao sabe falar HTTP (delega a
/// <see cref="IConvenioGateway"/>), nao sabe SQL (delega as portas de repositorio) e nao
/// conhece OpenTelemetry (delega a <see cref="IMetricasPort"/>).
/// </para>
/// <para>
/// <b>OCP</b>: a colecao de regras chega por injecao. Uma nova regra de negocio e um novo
/// registro no container - esta classe nao muda.
/// </para>
/// <para><b>DIP</b>: todos os colaboradores injetados sao interfaces ou tipos do dominio.</para>
/// </summary>
public sealed class AgendarConsultaUseCase(
    IConsultaRepository consultaRepository,
    IPacienteRepository pacienteRepository,
    IConvenioGateway convenioGateway,
    IDisponibilidadeEventPublisher eventPublisher,
    IDisponibilidadeCache disponibilidadeCache,
    IMetricasPort metricas,
    IEnumerable<IRegraAgendamento> regras,
    TimeProvider tempo,
    ILogger<AgendarConsultaUseCase> logger)
{
    private readonly IReadOnlyList<IRegraAgendamento> _regras =
        [.. regras.OrderBy(r => r.Ordem)];

    public async Task<ConsultaResponse> ExecutarAsync(
        AgendarConsultaRequest request, CancellationToken ct = default)
    {
        var paciente = await pacienteRepository.BuscarPorIdAsync(request.PacienteId, ct)
            ?? throw new RecursoNaoEncontradoException("Paciente", request.PacienteId);

        var consulta = new Consulta
        {
            PacienteId = request.PacienteId,
            ProfissionalId = request.ProfissionalId,
            DataHora = request.DataHora,
            Procedimento = request.Procedimento,
            CriadoEm = tempo.GetLocalNow().DateTime
        };

        await AplicarRegrasAsync(consulta, ct);

        var cobertura = await VerificarCoberturaAsync(paciente, request.Procedimento, ct);
        consulta.AplicarCobertura(cobertura);

        var salva = await consultaRepository.SalvarAsync(consulta, ct);
        await AposAgendarAsync(salva, ct);

        return ConsultaResponse.De(salva);
    }

    /// <summary>
    /// Executa as regras em ordem, sequencialmente e com fail-fast: a primeira violacao
    /// interrompe a cadeia (nao adianta bater no banco se o horario ja e invalido).
    /// </summary>
    private async Task AplicarRegrasAsync(Consulta consulta, CancellationToken ct)
    {
        foreach (var regra in _regras)
        {
            try
            {
                await regra.ValidarAsync(consulta, ct);
            }
            catch (RegraDeAgendamentoVioladaException e)
            {
                metricas.RegraDeAgendamentoViolada(e.Regra);
                throw;
            }
        }
    }

    /// <summary>
    /// Chamada ao servico EXTERNO, protegida por circuit breaker dentro do gateway.
    /// Paciente particular pula a verificacao - nao ha convenio a consultar.
    /// </summary>
    private async Task<StatusCobertura> VerificarCoberturaAsync(
        Paciente paciente, string procedimento, CancellationToken ct)
    {
        if (!paciente.PossuiConvenio)
        {
            return StatusCobertura.NaoAplicavel;
        }

        var resultado = await convenioGateway.VerificarCoberturaAsync(
            paciente.Id, paciente.ConvenioId!.Value, procedimento, ct);

        metricas.CoberturaVerificada(resultado);
        return resultado;
    }

    /// <summary>Efeitos colaterais pos-persistencia: invalida cache, notifica e mede.</summary>
    private async Task AposAgendarAsync(Consulta salva, CancellationToken ct)
    {
        metricas.ConsultaCriada(salva.CoberturaConvenio);

        if (salva.CoberturaConvenio is StatusCobertura.Indeterminada)
        {
            logger.LogWarning(
                "Consulta {ConsultaId} criada em modo degradado (convenio indisponivel): status={Status}",
                salva.Id, salva.Status);
        }

        await disponibilidadeCache.InvalidarAsync(
            salva.ProfissionalId, DateOnly.FromDateTime(salva.DataHora), ct);

        eventPublisher.Publicar(DisponibilidadeEvent.Ocupado(salva.ProfissionalId, salva.DataHora));
    }
}
