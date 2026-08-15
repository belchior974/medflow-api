using MedFlow.Application.Dtos;

namespace MedFlow.Application.Ports;

/// <summary>
/// PORTA de saida para o push em tempo real (DIP).
/// <para>
/// Os use cases publicam eventos sem saber que, por baixo, existe um
/// <c>System.Threading.Channels.Channel</c> alimentando um stream SSE. Trocar por
/// SignalR, Redis Pub/Sub ou Kafka exigiria apenas uma nova implementacao desta interface.
/// </para>
/// </summary>
public interface IDisponibilidadeEventPublisher
{
    void Publicar(DisponibilidadeEvent evento);

    /// <summary>
    /// Stream por assinante, consumido pelo endpoint SSE. Cada chamada cria o proprio
    /// canal, cancelado automaticamente quando o cliente desconecta.
    /// </summary>
    IAsyncEnumerable<DisponibilidadeEvent> AssinarAsync(
        long? profissionalId, CancellationToken ct = default);
}
