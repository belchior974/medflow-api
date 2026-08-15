using System.Data.Common;
using MedFlow.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MedFlow.Infrastructure.Persistence;

public sealed class DbConnectionFactory(IOptions<MedFlowOptions> options) : IDbConnectionFactory
{
    private readonly MedFlowOptions.BancoOptions _banco = options.Value.Banco;

    public string Provider => _banco.Provider.ToLowerInvariant();

    public DbConnection Criar() => Provider switch
    {
        "postgres" or "postgresql" => new NpgsqlConnection(_banco.ConnectionString),
        "sqlite" => new SqliteConnection(_banco.ConnectionString),
        _ => throw new InvalidOperationException(
            $"Provider de banco nao suportado: '{_banco.Provider}'. Use 'postgres' ou 'sqlite'.")
    };
}
