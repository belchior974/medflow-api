using System.Text.Json;
using MedFlow.Application.Dtos;
using MedFlow.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace MedFlow.UnitTests.Events;

/// <summary>
/// Prova que o transporte por Redis Pub/Sub distribui o evento entre instancias sem
/// duplicar a entrega, e que uma falha do Redis degrada para entrega local.
/// </summary>
public sealed class RedisDisponibilidadeEventPublisherTests
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly DisponibilidadeEventPublisher _local =
        new(NullLogger<DisponibilidadeEventPublisher>.Instance);

    /// <summary>Handler que o publisher registrou no Redis, para simular a chegada da mensagem.</summary>
    private Action<RedisChannel, RedisValue>? _aoReceber;

    public RedisDisponibilidadeEventPublisherTests()
    {
        _redis.GetSubscriber(Arg.Any<object?>()).Returns(_subscriber);

        _subscriber
            .When(s => s.Subscribe(Arg.Any<RedisChannel>(),
                Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
            .Do(chamada => _aoReceber = chamada.Arg<Action<RedisChannel, RedisValue>>());
    }

    private RedisDisponibilidadeEventPublisher Criar() =>
        new(_redis, _local, NullLogger<RedisDisponibilidadeEventPublisher>.Instance);

    private static DisponibilidadeEvent Evento() =>
        DisponibilidadeEvent.Liberado(1, new DateTime(2026, 8, 24, 9, 0, 0));

    [Fact(DisplayName = "publicar envia ao canal do Redis e NAO entrega direto no local (evita duplicata)")]
    public async Task PublicaNoRedisSemEntregaLocalDireta()
    {
        var publisher = Criar();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var recebidos = new List<DisponibilidadeEvent>();
        var assinatura = Task.Run(async () =>
        {
            await foreach (var e in publisher.AssinarAsync(null, cts.Token))
            {
                recebidos.Add(e);
            }
        }, CancellationToken.None);

        await Task.Delay(50, CancellationToken.None);
        publisher.Publicar(Evento());
        await Task.Delay(50, CancellationToken.None);

        // Foi para o Redis...
        _subscriber.Received(1).Publish(
            Arg.Is<RedisChannel>(c => c.ToString() == RedisDisponibilidadeEventPublisher.Canal),
            Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());

        // ...e nao foi entregue localmente pelo caminho direto: quem publicou recebe de
        // volta pelo proprio Redis, entao entregar aqui geraria evento duplicado.
        Assert.Empty(recebidos);

        try { await assinatura; } catch (OperationCanceledException) { /* esperado */ }
    }

    [Fact(DisplayName = "evento vindo do canal do Redis chega aos assinantes SSE locais")]
    public async Task EventoDoRedisChegaNoAssinanteLocal()
    {
        var publisher = Criar();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var recebidos = new List<DisponibilidadeEvent>();
        var assinatura = Task.Run(async () =>
        {
            await foreach (var e in publisher.AssinarAsync(null, cts.Token))
            {
                recebidos.Add(e);
                break;
            }
        }, CancellationToken.None);

        await Task.Delay(50, CancellationToken.None);

        // Simula a mensagem chegando do Redis - como se outra replica tivesse publicado.
        Assert.NotNull(_aoReceber);
        _aoReceber!(RedisChannel.Literal(RedisDisponibilidadeEventPublisher.Canal),
            JsonSerializer.Serialize(Evento(), JsonOpcoes));

        await assinatura;

        var recebido = Assert.Single(recebidos);
        Assert.Equal("LIBERADO", recebido.Tipo);
        Assert.Equal(1, recebido.ProfissionalId);
        Assert.Equal("09:00", recebido.Horario);
    }

    [Fact(DisplayName = "DEGRADACAO GRACIOSA: Redis fora do ar entrega localmente")]
    public async Task RedisForaDoArEntregaLocalmente()
    {
        _subscriber.Publish(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(_ => throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "connection refused"));

        var publisher = Criar();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var recebidos = new List<DisponibilidadeEvent>();
        var assinatura = Task.Run(async () =>
        {
            await foreach (var e in publisher.AssinarAsync(null, cts.Token))
            {
                recebidos.Add(e);
                break;
            }
        }, CancellationToken.None);

        await Task.Delay(50, CancellationToken.None);

        // Nao propaga a excecao: o agendamento nao pode falhar porque o push falhou.
        publisher.Publicar(Evento());

        await assinatura;
        Assert.Single(recebidos);
    }

    [Fact(DisplayName = "payload invalido no canal nao derruba a assinatura")]
    public void PayloadInvalidoNaoDerrubaAssinatura()
    {
        _ = Criar();

        Assert.NotNull(_aoReceber);
        _aoReceber!(RedisChannel.Literal(RedisDisponibilidadeEventPublisher.Canal), "{ nao e json valido");
    }
}
