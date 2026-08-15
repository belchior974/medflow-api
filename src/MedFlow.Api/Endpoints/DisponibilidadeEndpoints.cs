using System.Text.Json;
using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Application.UseCases;

namespace MedFlow.Api.Endpoints;

public static class DisponibilidadeEndpoints
{
    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapearDisponibilidade(this RouteGroupBuilder api)
    {
        var grupo = api.MapGroup("/profissionais/{id:long}/disponibilidade")
            .WithTags("Disponibilidade");

        // Request/response cacheado (L1 + L2)
        grupo.MapGet("/", async (
                long id,
                DateOnly? data,
                ConsultarDisponibilidadeUseCase useCase,
                CancellationToken ct) =>
            {
                var dia = data ?? DateOnly.FromDateTime(DateTime.Now);
                return Results.Ok(await useCase.ExecutarAsync(id, dia, ct));
            })
            .WithName("ConsultarDisponibilidade")
            .WithSummary("Horarios livres do profissional (servido pelo cache L1/L2)");

        // PUSH em tempo real via Server-Sent Events
        grupo.MapGet("/stream", (
                long id,
                IDisponibilidadeEventPublisher publisher,
                HttpContext contexto,
                CancellationToken ct) =>
            EscreverStreamSseAsync(id, publisher, contexto, ct))
            .WithName("StreamDisponibilidade")
            .WithSummary("Stream SSE: empurra vagas liberadas/ocupadas em tempo real")
            .ExcludeFromDescription();

        return api;
    }

    /// <summary>
    /// Escreve o stream SSE na mao.
    /// <para>
    /// Cada requisicao SSE ocupa apenas a continuacao assincrona enquanto espera - nao
    /// ha uma thread bloqueada por cliente conectado, que era exatamente o ganho do
    /// WebFlux na versao Spring. Aqui isso e o comportamento padrao do ASP.NET Core.
    /// </para>
    /// </summary>
    private static async Task EscreverStreamSseAsync(
        long profissionalId,
        IDisponibilidadeEventPublisher publisher,
        HttpContext contexto,
        CancellationToken ct)
    {
        contexto.Response.Headers.ContentType = "text/event-stream";
        contexto.Response.Headers.CacheControl = "no-cache";
        contexto.Response.Headers.Connection = "keep-alive";

        // Heartbeat evita que proxies derrubem a conexao ociosa e deixa visivel,
        // no video, que o canal continua aberto.
        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(20));
        using var origem = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var tarefaHeartbeat = Task.Run(async () =>
        {
            try
            {
                while (await heartbeat.WaitForNextTickAsync(origem.Token))
                {
                    await EnviarAsync(contexto, DisponibilidadeEvent.Heartbeat(profissionalId), origem.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // desconexao normal do cliente
            }
        }, origem.Token);

        try
        {
            await foreach (var evento in publisher.AssinarAsync(profissionalId, origem.Token))
            {
                await EnviarAsync(contexto, evento, origem.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // cliente desconectou
        }
        finally
        {
            await origem.CancelAsync();
            await Task.WhenAny(tarefaHeartbeat, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));
        }
    }

    private static async Task EnviarAsync(
        HttpContext contexto, DisponibilidadeEvent evento, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evento, JsonOpcoes);
        await contexto.Response.WriteAsync($"event: {evento.Tipo}\ndata: {json}\n\n", ct);
        await contexto.Response.Body.FlushAsync(ct);
    }
}
