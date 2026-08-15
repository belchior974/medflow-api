using System.Data.Common;

namespace MedFlow.Infrastructure.Persistence;

/// <summary>
/// Abstrai o provider do banco para que os repositorios Dapper nao dependam de
/// Npgsql nem de Microsoft.Data.Sqlite diretamente.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>"postgres" ou "sqlite".</summary>
    string Provider { get; }

    DbConnection Criar();
}
