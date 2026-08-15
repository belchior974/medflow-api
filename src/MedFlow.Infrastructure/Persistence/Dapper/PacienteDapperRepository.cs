using Dapper;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Infrastructure.Persistence.Dapper;

internal sealed class PacienteDapperRepository(IDbConnectionFactory fabrica) : IPacienteRepository
{
    private const string Colunas = """
        id AS Id, nome AS Nome, cpf AS Cpf, data_nascimento AS DataNascimento,
        email AS Email, telefone AS Telefone, convenio_id AS ConvenioId
        """;

    public async Task<Paciente> SalvarAsync(Paciente paciente, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        const string insert = """
            INSERT INTO paciente (nome, cpf, data_nascimento, email, telefone, convenio_id)
            VALUES (@Nome, @Cpf, @DataNascimento, @Email, @Telefone, @ConvenioId)
            RETURNING id
            """;

        var parametros = new
        {
            paciente.Nome,
            paciente.Cpf,
            DataNascimento = paciente.DataNascimento.ToDateTime(TimeOnly.MinValue),
            paciente.Email,
            paciente.Telefone,
            paciente.ConvenioId
        };

        paciente.Id = await conexao.ExecuteScalarAsync<long>(
            new CommandDefinition(insert, parametros, cancellationToken: ct));

        return paciente;
    }

    public async Task<Paciente?> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"SELECT {Colunas} FROM paciente WHERE id = @id";
        var linha = await conexao.QueryFirstOrDefaultAsync<PacienteRow>(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));

        return linha?.ParaDominio();
    }

    public async Task<IReadOnlyList<Paciente>> ListarTodosAsync(CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"SELECT {Colunas} FROM paciente ORDER BY id";
        var linhas = await conexao.QueryAsync<PacienteRow>(
            new CommandDefinition(sql, cancellationToken: ct));

        return [.. linhas.Select(l => l.ParaDominio())];
    }
}
