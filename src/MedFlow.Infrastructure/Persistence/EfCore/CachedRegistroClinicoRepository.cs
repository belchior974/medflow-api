using MedFlow.Application.Ports;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Infrastructure.Persistence.EfCore;

/// <summary>
/// DECORADOR de cache do prontuario.
/// <para>
/// Este e o equivalente .NET das anotacoes <c>@Cacheable</c>, <c>@CachePut</c> e
/// <c>@CacheEvict</c> do Modulo 5 - e, na nossa avaliacao, uma solucao melhor:
/// </para>
/// <list type="bullet">
///   <item>e explicito: da para ler o que e cacheado e quando e invalidado;</item>
///   <item>nao depende de proxy dinamico, entao <b>funciona em chamadas internas</b>
///         (a famosa armadilha do self-invocation no Spring AOP);</item>
///   <item>e testavel sem subir container de DI.</item>
/// </list>
/// <para>
/// LSP em acao: quem depende de <see cref="IRegistroClinicoRepository"/> nao percebe
/// diferenca alguma entre a versao com e sem cache.
/// </para>
/// </summary>
internal sealed class CachedRegistroClinicoRepository(
    RegistroClinicoRepository interno,
    ICachePort cache) : IRegistroClinicoRepository
{
    public const string CacheResumo = "historicoClinico";
    public const string CacheRegistro = "registroClinico";

    /// <summary>Escrita: alimenta o cache do item e invalida o resumo daquele paciente.</summary>
    public async Task<RegistroClinico> SalvarAsync(RegistroClinico registro, CancellationToken ct = default)
    {
        var salvo = await interno.SalvarAsync(registro, ct);

        await cache.GravarAsync(CacheRegistro, salvo.Id.ToString(), salvo, ct);
        await cache.InvalidarAsync(CacheResumo, salvo.PacienteId.ToString(), ct);

        return salvo;
    }

    public async Task<RegistroClinico?> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        var chave = id.ToString();

        var emCache = await cache.ObterAsync<RegistroClinico>(CacheRegistro, chave, ct);
        if (emCache is not null)
        {
            return emCache;
        }

        var doBanco = await interno.BuscarPorIdAsync(id, ct);
        if (doBanco is not null)
        {
            await cache.GravarAsync(CacheRegistro, chave, doBanco, ct);
        }

        return doBanco;
    }

    /// <summary>Busca dinamica NAO e cacheada: o espaco de combinacoes de filtro e grande
    /// demais e a taxa de acerto seria proxima de zero.</summary>
    public Task<IReadOnlyList<RegistroClinico>> BuscarPorFiltroAsync(
        FiltroHistoricoClinico filtro, CancellationToken ct = default) =>
        interno.BuscarPorFiltroAsync(filtro, ct);

    /// <summary>Leitura quente: projecao + cache multinivel.</summary>
    public async Task<IReadOnlyList<RegistroClinicoResumo>> ResumoPorPacienteAsync(
        long pacienteId, CancellationToken ct = default)
    {
        var chave = pacienteId.ToString();

        var emCache = await cache.ObterAsync<List<RegistroClinicoResumo>>(CacheResumo, chave, ct);
        if (emCache is not null)
        {
            return emCache;
        }

        var doBanco = await interno.ResumoPorPacienteAsync(pacienteId, ct);
        await cache.GravarAsync(CacheResumo, chave, doBanco.ToList(), ct);

        return doBanco;
    }
}
