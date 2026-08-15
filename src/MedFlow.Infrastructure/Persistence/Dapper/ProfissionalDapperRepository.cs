using Dapper;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Infrastructure.Persistence.Dapper;

internal sealed class ProfissionalDapperRepository(IDbConnectionFactory fabrica) : IProfissionalRepository
{
    private const string Colunas = """
        id AS Id, nome AS Nome, especialidade AS Especialidade, crm AS Crm, ativo AS Ativo
        """;

    public async Task<Profissional> SalvarAsync(Profissional profissional, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        const string insert = """
            INSERT INTO profissional (nome, especialidade, crm, ativo)
            VALUES (@Nome, @Especialidade, @Crm, @Ativo)
            RETURNING id
            """;

        var parametros = new
        {
            profissional.Nome,
            profissional.Especialidade,
            profissional.Crm,
            profissional.Ativo
        };

        profissional.Id = await conexao.ExecuteScalarAsync<long>(
            new CommandDefinition(insert, parametros, cancellationToken: ct));

        return profissional;
    }

    public async Task<Profissional?> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"SELECT {Colunas} FROM profissional WHERE id = @id";
        var linha = await conexao.QueryFirstOrDefaultAsync<ProfissionalRow>(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));

        return linha?.ParaDominio();
    }

    public async Task<IReadOnlyList<Profissional>> ListarTodosAsync(CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"SELECT {Colunas} FROM profissional ORDER BY id";
        var linhas = await conexao.QueryAsync<ProfissionalRow>(
            new CommandDefinition(sql, cancellationToken: ct));

        return [.. linhas.Select(l => l.ParaDominio())];
    }
}
