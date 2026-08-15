using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Application.UseCases;

/// <summary>
/// Transicoes de estado da consulta.
/// <para>
/// Note que a decisao "posso cancelar?" nao esta aqui: ela vive na entidade
/// <see cref="Consulta"/>. Este use case apenas orquestra carregar -&gt; transicionar
/// -&gt; persistir -&gt; invalidar cache -&gt; notificar (SRP).
/// </para>
/// </summary>
public sealed class GerenciarConsultaUseCase(
    IConsultaRepository consultaRepository,
    IDisponibilidadeCache disponibilidadeCache,
    IDisponibilidadeEventPublisher eventPublisher,
    IMetricasPort metricas)
{
    public async Task<ConsultaResponse> CancelarAsync(long consultaId, CancellationToken ct = default)
    {
        var consulta = await CarregarAsync(consultaId, ct);
        var cancelada = await consultaRepository.SalvarAsync(consulta.Cancelar(), ct);

        metricas.ConsultaCancelada();

        // Cancelou -> o slot voltou a ficar livre: invalida o cache e empurra o
        // evento para quem estiver ouvindo o stream SSE.
        await disponibilidadeCache.InvalidarAsync(
            cancelada.ProfissionalId, DateOnly.FromDateTime(cancelada.DataHora), ct);

        eventPublisher.Publicar(
            DisponibilidadeEvent.Liberado(cancelada.ProfissionalId, cancelada.DataHora));

        return ConsultaResponse.De(cancelada);
    }

    public async Task<ConsultaResponse> ConfirmarAsync(long consultaId, CancellationToken ct = default)
    {
        var consulta = await CarregarAsync(consultaId, ct);
        var confirmada = await consultaRepository.SalvarAsync(consulta.Confirmar(), ct);

        metricas.ConsultaConfirmada();
        return ConsultaResponse.De(confirmada);
    }

    public async Task<ConsultaResponse> BuscarPorIdAsync(long consultaId, CancellationToken ct = default) =>
        ConsultaResponse.De(await CarregarAsync(consultaId, ct));

    public async Task<IReadOnlyList<ConsultaResponse>> ListarPorPacienteAsync(
        long pacienteId, CancellationToken ct = default)
    {
        var consultas = await consultaRepository.BuscarPorPacienteAsync(pacienteId, ct);
        return [.. consultas.Select(ConsultaResponse.De)];
    }

    private async Task<Consulta> CarregarAsync(long consultaId, CancellationToken ct) =>
        await consultaRepository.BuscarPorIdAsync(consultaId, ct)
        ?? throw new RecursoNaoEncontradoException("Consulta", consultaId);
}
