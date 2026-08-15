using System.Threading.Channels;
using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using Microsoft.Extensions.Logging;

namespace MedFlow.Infrastructure.Events;

/// <summary>
/// ADAPTER de push em tempo real usando <c>System.Threading.Channels</c>.
/// <para>
/// Equivalente, na camada de agendamento, ao papel dos <i>Change Streams</i> do Modulo 3
/// e do <c>Sinks.Many</c> do Reactor na versao Spring: quando uma consulta e cancelada,
/// o slot liberado e empurrado para todos os clientes conectados ao endpoint SSE, sem polling.
/// </para>
/// <para>
/// Cada assinante recebe o proprio canal <c>BoundedChannel</c> com
/// <c>DropOldest</c>: um cliente lento nunca bloqueia os demais nem o publicador.
/// </para>
/// </summary>
public sealed class DisponibilidadeEventPublisher(ILogger<DisponibilidadeEventPublisher> logger)
    : IDisponibilidadeEventPublisher
{
    // C# 13 / .NET 9: System.Threading.Lock e mais rapido que travar em um object
    // e o compilador impede o erro classico de usa-lo com Monitor.Enter manualmente.
    private readonly Lock _trava = new();
    private readonly List<Channel<DisponibilidadeEvent>> _assinantes = [];

    public void Publicar(DisponibilidadeEvent evento)
    {
        Channel<DisponibilidadeEvent>[] destinos;
        lock (_trava)
        {
            destinos = [.. _assinantes];
        }

        if (destinos.Length == 0)
        {
            logger.LogDebug("Nenhum assinante ativo para o evento {Tipo}", evento.Tipo);
            return;
        }

        foreach (var canal in destinos)
        {
            canal.Writer.TryWrite(evento);
        }
    }

    public async IAsyncEnumerable<DisponibilidadeEvent> AssinarAsync(
        long? profissionalId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var canal = Channel.CreateBounded<DisponibilidadeEvent>(
            new BoundedChannelOptions(128)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

        lock (_trava)
        {
            _assinantes.Add(canal);
        }

        logger.LogDebug("Novo assinante SSE (profissional {ProfissionalId})", profissionalId);

        try
        {
            await foreach (var evento in canal.Reader.ReadAllAsync(ct))
            {
                if (profissionalId is null || evento.ProfissionalId == profissionalId.Value)
                {
                    yield return evento;
                }
            }
        }
        finally
        {
            lock (_trava)
            {
                _assinantes.Remove(canal);
            }

            canal.Writer.TryComplete();
            logger.LogDebug("Assinante SSE desconectado (profissional {ProfissionalId})", profissionalId);
        }
    }
}
