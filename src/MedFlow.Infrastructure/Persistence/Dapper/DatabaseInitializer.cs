using Dapper;
using MedFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// Cria o schema e a massa inicial no startup.
/// <para>
/// Equivale ao <c>ConnectionFactoryInitializer</c> da versao Spring. Roda como
/// <see cref="IHostedService"/>, ou seja, antes de o servidor comecar a aceitar
/// requisicoes.
/// </para>
/// </summary>
public sealed class DatabaseInitializer(
    IDbConnectionFactory fabrica,
    IOptions<MedFlowOptions> options,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Banco.AutoInicializarSchema)
        {
            logger.LogInformation("Inicializacao automatica do schema desativada.");
            return;
        }

        logger.LogInformation("Inicializando schema ({Provider})...", fabrica.Provider);

        await using var conexao = fabrica.Criar();
        await conexao.ExecuteAsync(new CommandDefinition(
            SchemaScripts.Para(fabrica.Provider), cancellationToken: cancellationToken));
        await conexao.ExecuteAsync(new CommandDefinition(
            SchemaScripts.Seed, cancellationToken: cancellationToken));

        logger.LogInformation("Schema pronto.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
