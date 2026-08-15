namespace MedFlow.Application.Ports;

/// <summary>
/// PORTA de saida para o cache multinivel (DIP).
/// <para>
/// O use case pede "o snapshot da agenda"; ele nao sabe que existe Caffeine, Redis,
/// <c>IMemoryCache</c> ou <c>HybridCache</c> por baixo. Trocar a implementacao nao toca
/// em uma linha de codigo de negocio.
/// </para>
/// </summary>
public interface ICachePort
{
    ValueTask<T?> ObterAsync<T>(string cache, string chave, CancellationToken ct = default)
        where T : class;

    ValueTask<T> GravarAsync<T>(string cache, string chave, T valor, CancellationToken ct = default)
        where T : class;

    ValueTask InvalidarAsync(string cache, string chave, CancellationToken ct = default);

    ValueTask InvalidarCacheAsync(string cache, CancellationToken ct = default);
}
