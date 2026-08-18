using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MedFlow.Infrastructure.Cache;

/// <summary>
/// Resolve a conexao com o Redis - que e OPCIONAL - no momento da construcao do servico.
/// <para>
/// Existe para que a connection string nao seja lida em tempo de REGISTRO no DI. No
/// modelo de hosting minimo do .NET 6+, fontes de configuracao adicionadas depois do
/// <c>Program.cs</c> (o <c>WebApplicationFactory</c> dos testes de integracao, por
/// exemplo) so entram em vigor no <c>Build()</c>. Ler a chave antes disso congela o
/// valor do appsettings.json e torna qualquer override silenciosamente invisivel.
/// </para>
/// <para>
/// Connection string vazia significa "sem Redis": o cache degrada para L1 apenas, sem
/// quebrar. Um Redis configurado mas fora do ar tambem nao impede o startup, graças a
/// <see cref="ConfigurationOptions.AbortOnConnectFail"/> desligado.
/// </para>
/// </summary>
public sealed class RedisConnectionProvider
{
    public RedisConnectionProvider(IConfiguration configuracao, ILogger<RedisConnectionProvider> logger)
    {
        var conexao = configuracao.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(conexao))
        {
            logger.LogInformation("Redis nao configurado - o cache vai operar apenas com L1.");
            return;
        }

        var opcoes = ConfigurationOptions.Parse(conexao);
        // CRUCIAL para a degradacao graciosa: sem isso a aplicacao NAO SOBE
        // quando o Redis esta fora do ar.
        opcoes.AbortOnConnectFail = false;
        opcoes.ConnectTimeout = 2_000;
        opcoes.SyncTimeout = 2_000;

        Conexao = ConnectionMultiplexer.Connect(opcoes);
    }

    /// <summary><c>null</c> quando nao ha Redis configurado.</summary>
    public IConnectionMultiplexer? Conexao { get; }
}
