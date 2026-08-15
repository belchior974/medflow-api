using System.Text.Json;
using MedFlow.Application.Ports;
using MedFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MedFlow.Infrastructure.Cache;

/// <summary>
/// CACHE MULTINIVEL: L1 em memoria (<see cref="IMemoryCache"/>) + L2 distribuido (Redis).
/// <code>
///   Obter:   L1  --miss--&gt;  L2 Redis  --miss--&gt;  chamador consulta o banco
///                              |
///                              +-- promove para L1
///   Gravar:  escreve em L1 e L2
///   Invalidar: remove de L1 e L2
/// </code>
/// <para>
/// <b>Resiliencia do proprio cache</b>: qualquer falha do Redis e capturada e logada, e a
/// operacao degrada para L1 apenas. Um Redis fora do ar deixa a API mais lenta, nunca
/// indisponivel.
/// </para>
/// <para>
/// Simplificacao notavel em relacao a versao Spring: la foi preciso um envelope com o nome
/// do tipo, porque a interface <c>org.springframework.cache.Cache</c> e destipada e o
/// serializador padrao nao grava o tipo de <c>record</c>s. Aqui a porta e generica
/// (<c>ObterAsync&lt;T&gt;</c>), o tipo alvo e conhecido em tempo de compilacao e o JSON
/// gravado no Redis e o objeto puro - legivel no <c>redis-cli</c>, sem metadados.
/// </para>
/// </summary>
public sealed class TwoLevelCache : ICachePort
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _l1;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IMetricasPort _metricas;
    private readonly ILogger<TwoLevelCache> _logger;
    private readonly TimeSpan _ttlL1;
    private readonly TimeSpan _ttlL2;

    public TwoLevelCache(
        IMemoryCache l1,
        IMetricasPort metricas,
        IOptions<MedFlowOptions> options,
        ILogger<TwoLevelCache> logger,
        IConnectionMultiplexer? redis = null)
    {
        _l1 = l1;
        _redis = redis;
        _metricas = metricas;
        _logger = logger;
        _ttlL1 = TimeSpan.FromSeconds(options.Value.Cache.L1TtlSegundos);
        _ttlL2 = TimeSpan.FromSeconds(options.Value.Cache.L2TtlSegundos);
    }

    public async ValueTask<T?> ObterAsync<T>(string cache, string chave, CancellationToken ct = default)
        where T : class
    {
        var chaveCompleta = ChaveCompleta(cache, chave);

        if (_l1.TryGetValue(chaveCompleta, out T? doL1) && doL1 is not null)
        {
            _metricas.CacheHit(cache, "L1");
            return doL1;
        }

        var json = await LerDoRedisAsync(chaveCompleta, cache);
        if (json is null)
        {
            return null;
        }

        try
        {
            var valor = JsonSerializer.Deserialize<T>(json, JsonOpcoes);
            if (valor is null)
            {
                return null;
            }

            _metricas.CacheHit(cache, "L2");
            GravarEmL1(chaveCompleta, valor);   // promove para L1
            return valor;
        }
        catch (JsonException e)
        {
            // Payload gravado por uma versao anterior do contrato: trata como miss.
            _logger.LogWarning(e, "Payload invalido em {Chave} - descartando do cache", chaveCompleta);
            return null;
        }
    }

    public async ValueTask<T> GravarAsync<T>(
        string cache, string chave, T valor, CancellationToken ct = default) where T : class
    {
        var chaveCompleta = ChaveCompleta(cache, chave);
        GravarEmL1(chaveCompleta, valor);

        if (_redis is null)
        {
            return valor;
        }

        try
        {
            var json = JsonSerializer.Serialize(valor, JsonOpcoes);
            await _redis.GetDatabase().StringSetAsync(chaveCompleta, json, _ttlL2);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogWarning(
                "Redis (L2) indisponivel na escrita de {Chave} - mantendo apenas L1. Causa: {Causa}",
                chaveCompleta, e.Message);
        }

        return valor;
    }

    public async ValueTask InvalidarAsync(string cache, string chave, CancellationToken ct = default)
    {
        var chaveCompleta = ChaveCompleta(cache, chave);
        _l1.Remove(chaveCompleta);

        if (_redis is null)
        {
            return;
        }

        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(chaveCompleta);
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogWarning("Falha ao invalidar {Chave} no Redis: {Causa}", chaveCompleta, e.Message);
        }
    }

    public async ValueTask InvalidarCacheAsync(string cache, CancellationToken ct = default)
    {
        if (_redis is null)
        {
            return;
        }

        try
        {
            var padrao = $"{Prefixo(cache)}*";
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var servidor = _redis.GetServer(endpoint);
                foreach (var chave in servidor.Keys(pattern: padrao))
                {
                    _l1.Remove(chave.ToString());
                    await _redis.GetDatabase().KeyDeleteAsync(chave);
                }
            }
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogWarning("Falha ao limpar o cache {Cache} no Redis: {Causa}", cache, e.Message);
        }
    }

    private async Task<string?> LerDoRedisAsync(string chaveCompleta, string cache)
    {
        if (_redis is null)
        {
            _metricas.CacheMiss(cache);
            return null;
        }

        try
        {
            var valor = await _redis.GetDatabase().StringGetAsync(chaveCompleta);
            return valor.HasValue ? valor.ToString() : null;
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _logger.LogWarning(
                "Redis (L2) indisponivel na leitura de {Chave} - degradando para L1. Causa: {Causa}",
                chaveCompleta, e.Message);
            return null;
        }
    }

    private void GravarEmL1<T>(string chaveCompleta, T valor) =>
        _l1.Set(chaveCompleta, valor, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _ttlL1,
            Size = 1
        });

    private static string ChaveCompleta(string cache, string chave) => Prefixo(cache) + chave;

    private static string Prefixo(string cache) => $"medflow:{cache}:";
}
