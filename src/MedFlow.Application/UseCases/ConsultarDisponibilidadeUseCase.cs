using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Domain.Model.Enums;
using MedFlow.Domain.Repositories;
using MedFlow.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace MedFlow.Application.UseCases;

/// <summary>
/// Leitura de alta frequencia -&gt; caminho ideal para o cache multinivel.
/// <para>Fluxo: L1 em memoria -&gt; L2 Redis -&gt; banco.</para>
/// </summary>
public sealed class ConsultarDisponibilidadeUseCase(
    IConsultaRepository consultaRepository,
    ICachePort cache,
    IMetricasPort metricas,
    ParametrosAgenda parametros,
    ILogger<ConsultarDisponibilidadeUseCase> logger) : IDisponibilidadeCache
{
    public const string Cache = "disponibilidade";

    public async Task<DisponibilidadeSnapshot> ExecutarAsync(
        long profissionalId, DateOnly data, CancellationToken ct = default)
    {
        var chave = Chave(profissionalId, data);

        var emCache = await cache.ObterAsync<DisponibilidadeSnapshot>(Cache, chave, ct);
        if (emCache is not null)
        {
            return emCache;
        }

        metricas.CacheMiss(Cache);
        logger.LogDebug("Cache MISS para {Chave} - calculando disponibilidade no banco", chave);

        var snapshot = await CalcularAsync(profissionalId, data, ct);
        return await cache.GravarAsync(Cache, chave, snapshot, ct);
    }

    /// <summary>Invalida L1 e L2 do slot afetado (chamado ao agendar/cancelar).</summary>
    public ValueTask InvalidarAsync(long profissionalId, DateOnly data, CancellationToken ct = default) =>
        cache.InvalidarAsync(Cache, Chave(profissionalId, data), ct);

    private async Task<DisponibilidadeSnapshot> CalcularAsync(
        long profissionalId, DateOnly data, CancellationToken ct)
    {
        var inicio = data.ToDateTime(TimeOnly.MinValue);
        var fim = data.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var consultas = await consultaRepository.BuscarPorProfissionalEIntervaloAsync(
            profissionalId, inicio, fim, ct);

        var ocupados = consultas
            .Where(c => c.Status != StatusConsulta.Cancelada)
            .Select(c => c.DataHora)
            .ToHashSet();

        var todos = parametros.SlotsDoDia(data);
        var livres = todos
            .Where(slot => !ocupados.Contains(slot))
            .Select(slot => slot.ToString("HH:mm"))
            .ToList();

        return new DisponibilidadeSnapshot(profissionalId, data, livres, todos.Count);
    }

    private static string Chave(long profissionalId, DateOnly data) =>
        $"{profissionalId}:{data:yyyy-MM-dd}";
}
