using System.Text.Json;
using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MedFlow.Infrastructure.Events;

/// <summary>
/// ADAPTER de push em tempo real distribuido, via Redis Pub/Sub.
/// <para>
/// Resolve a limitacao do <see cref="DisponibilidadeEventPublisher"/>, cujo transporte e
/// in-process: com mais de uma replica da API no ar, um cliente SSE conectado na replica
/// A nunca receberia um evento publicado na replica B. O cache L2 ja era compartilhado
/// pelo Redis; os eventos passam a ser tambem.
/// </para>
/// <para>
/// <b>Composicao, nao substituicao</b>: o fan-out para os clientes SSE continua sendo do
/// <see cref="DisponibilidadeEventPublisher"/>. Esta classe apenas troca o transporte -
/// publica no canal do Redis e, ao receber do canal, delega o fan-out local. Como o
/// proprio publicador tambem e assinante, quem publicou recebe de volta pelo Redis; por
/// isso <see cref="Publicar"/> NAO entrega localmente, o que evitaria evento duplicado.
/// </para>
/// <para>
/// <b>Degradacao graciosa</b>: se o Redis estiver fora do ar, a publicacao cai para
/// entrega local. A instancia perde o alcance entre replicas, nunca o push em si.
/// </para>
/// <para>
/// Esta troca exercita o DIP: nenhum use case muda, porque todos dependem apenas de
/// <see cref="IDisponibilidadeEventPublisher"/>.
/// </para>
/// </summary>
public sealed class RedisDisponibilidadeEventPublisher : IDisponibilidadeEventPublisher, IDisposable
{
    /// <summary>Canal do Redis Pub/Sub. Nao e uma chave: nao aparece em KEYS nem expira.</summary>
    public const string Canal = "medflow:eventos:disponibilidade";

    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly DisponibilidadeEventPublisher _local;
    private readonly ILogger<RedisDisponibilidadeEventPublisher> _logger;
    private readonly RedisChannel _canal = RedisChannel.Literal(Canal);

    public RedisDisponibilidadeEventPublisher(
        IConnectionMultiplexer redis,
        DisponibilidadeEventPublisher local,
        ILogger<RedisDisponibilidadeEventPublisher> logger)
    {
        _redis = redis;
        _local = local;
        _logger = logger;

        try
        {
            _redis.GetSubscriber().Subscribe(_canal, AoReceber);
            _logger.LogInformation("Push em tempo real usando Redis Pub/Sub no canal {Canal}", Canal);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            // Nao impede o startup: o push continua funcionando dentro desta instancia.
            _logger.LogWarning(
                "Nao foi possivel assinar {Canal} no Redis - o push fica restrito a esta instancia. Causa: {Causa}",
                Canal, e.Message);
        }
    }

    public void Publicar(DisponibilidadeEvent evento)
    {
        try
        {
            var json = JsonSerializer.Serialize(evento, JsonOpcoes);
            _redis.GetSubscriber().Publish(_canal, json);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogWarning(
                "Redis indisponivel ao publicar {Tipo} - entregando apenas aos assinantes locais. Causa: {Causa}",
                evento.Tipo, e.Message);
            _local.Publicar(evento);
        }
    }

    public IAsyncEnumerable<DisponibilidadeEvent> AssinarAsync(
        long? profissionalId, CancellationToken ct = default) =>
        _local.AssinarAsync(profissionalId, ct);

    private void AoReceber(RedisChannel _, RedisValue valor)
    {
        if (!valor.HasValue)
        {
            return;
        }

        try
        {
            var evento = JsonSerializer.Deserialize<DisponibilidadeEvent>(valor!, JsonOpcoes);
            if (evento is not null)
            {
                _local.Publicar(evento);
            }
        }
        catch (JsonException e)
        {
            // Payload de uma versao anterior do contrato: descarta sem derrubar a assinatura.
            _logger.LogWarning(e, "Evento invalido recebido em {Canal} - descartando", Canal);
        }
    }

    public void Dispose()
    {
        try
        {
            _redis.GetSubscriber().Unsubscribe(_canal, AoReceber);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogDebug("Falha ao cancelar a assinatura de {Canal}: {Causa}", Canal, e.Message);
        }
    }
}
