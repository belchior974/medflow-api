using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Infrastructure.Cache;
using MedFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace MedFlow.UnitTests.Cache;

/// <summary>
/// Prova o comportamento de cada camada do cache multinivel isoladamente, incluindo o
/// cenario mais importante para a resiliencia: <b>Redis fora do ar</b>.
/// </summary>
public sealed class TwoLevelCacheTests
{
    private const string NomeCache = "disponibilidade";

    private readonly IMetricasPort _metricas = Substitute.For<IMetricasPort>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly Dictionary<string, string> _redisFake = [];

    private static readonly IOptions<MedFlowOptions> Opcoes = Options.Create(new MedFlowOptions
    {
        Cache = new MedFlowOptions.CacheOptions { L1TtlSegundos = 30, L2TtlSegundos = 300 }
    });

    public TwoLevelCacheTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(chamada =>
            {
                var chave = chamada.Arg<RedisKey>().ToString();
                return Task.FromResult(_redisFake.TryGetValue(chave, out var v)
                    ? (RedisValue)v
                    : RedisValue.Null);
            });

        // A sobrecarga que TwoLevelCache.GravarAsync realmente resolve inclui o
        // parametro "bool keepTtl"; sem ele o stub nunca casa e o Redis falso fica vazio.
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(chamada =>
            {
                _redisFake[chamada.Arg<RedisKey>().ToString()] = chamada.Arg<RedisValue>().ToString();
                return Task.FromResult(true);
            });

        _db.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(chamada => Task.FromResult(_redisFake.Remove(chamada.Arg<RedisKey>().ToString())));
    }

    private TwoLevelCache CriarCache(IConnectionMultiplexer? redis) =>
        new(new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            _metricas, Opcoes, NullLogger<TwoLevelCache>.Instance, redis);

    private static DisponibilidadeSnapshot Snapshot() =>
        new(1, new DateOnly(2026, 8, 17), ["09:00", "09:30"], 20);

    [Fact(DisplayName = "MISS -> grava em L1 e L2; segunda leitura e HIT em L1")]
    public async Task GravaNasDuasCamadasELeDeL1()
    {
        var cache = CriarCache(_redis);

        Assert.Null(await cache.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17"));
        _metricas.Received(1).CacheMiss(NomeCache);

        await cache.GravarAsync(NomeCache, "1:2026-08-17", Snapshot());

        var lido = await cache.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17");

        Assert.NotNull(lido);
        Assert.Equal(2, lido.HorariosLivres.Count);
        _metricas.Received(1).CacheHit(NomeCache, "L1");
        Assert.True(_redisFake.ContainsKey("medflow:disponibilidade:1:2026-08-17"),
            "deveria ter gravado tambem no L2");
    }

    [Fact(DisplayName = "L1 vazio mas L2 preenchido: le do Redis, PROMOVE para L1 e devolve tipado")]
    public async Task PromoveDeL2ParaL1()
    {
        var primeira = CriarCache(_redis);
        await primeira.GravarAsync(NomeCache, "1:2026-08-17", Snapshot());

        // Simula outra instancia da aplicacao: L1 local vazio, Redis compartilhado cheio.
        var outraInstancia = CriarCache(_redis);

        var lido = await outraInstancia.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17");

        Assert.NotNull(lido);
        Assert.Equal(20, lido.TotalSlots);
        Assert.Equal("09:00", lido.HorariosLivres[0]);
        _metricas.Received(1).CacheHit(NomeCache, "L2");

        // Agora ja esta no L1 dessa instancia.
        await outraInstancia.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17");
        _metricas.Received(1).CacheHit(NomeCache, "L1");
    }

    [Fact(DisplayName = "invalidar remove das duas camadas")]
    public async Task InvalidarRemoveDasDuasCamadas()
    {
        var cache = CriarCache(_redis);
        await cache.GravarAsync(NomeCache, "1:2026-08-17", Snapshot());

        await cache.InvalidarAsync(NomeCache, "1:2026-08-17");

        Assert.Null(await cache.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17"));
        Assert.Empty(_redisFake);
    }

    [Fact(DisplayName = "RESILIENCIA: Redis lancando excecao nao propaga erro - degrada para L1")]
    public async Task RedisComFalhaDegradaParaL1()
    {
        // A sobrecarga que TwoLevelCache.GravarAsync realmente resolve inclui o
        // parametro "bool keepTtl"; sem ele o stub nunca casa e o Redis falso fica vazio.
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns<Task<bool>>(_ => throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "connection refused"));

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<RedisValue>>(_ => throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "connection refused"));

        var cache = CriarCache(_redis);

        // Nenhuma destas operacoes pode lancar excecao.
        await cache.GravarAsync(NomeCache, "1:2026-08-17", Snapshot());
        var lido = await cache.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17");

        Assert.NotNull(lido);   // veio do L1
        _metricas.Received(1).CacheHit(NomeCache, "L1");
    }

    [Fact(DisplayName = "sem Redis registrado, o cache funciona apenas com L1")]
    public async Task FuncionaSemRedis()
    {
        var cache = CriarCache(redis: null);

        await cache.GravarAsync(NomeCache, "1:2026-08-17", Snapshot());
        var lido = await cache.ObterAsync<DisponibilidadeSnapshot>(NomeCache, "1:2026-08-17");

        Assert.NotNull(lido);
        _metricas.Received(1).CacheHit(NomeCache, "L1");
    }
}
