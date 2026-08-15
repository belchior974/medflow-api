using Dapper;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// ADAPTER Dapper para o agendamento - o caminho quente da API.
/// <para>
/// Dapper (e nao EF Core) porque estas consultas tem forma fixa, sao executadas com
/// altissima frequencia e nao precisam de change tracking. E o equivalente da escolha
/// por R2DBC na versao Spring, pelo mesmo motivo: performance no caminho critico.
/// </para>
/// <para>
/// As colunas sao aliasadas explicitamente (<c>paciente_id AS PacienteId</c>) em vez de
/// depender de configuracao global de mapeamento - o SQL fica autoexplicativo e nao ha
/// estado estatico compartilhado.
/// </para>
/// </summary>
internal sealed class ConsultaDapperRepository(IDbConnectionFactory fabrica) : IConsultaRepository
{
    private const string Colunas = """
        id AS Id, paciente_id AS PacienteId, profissional_id AS ProfissionalId,
        data_hora AS DataHora, status AS Status, procedimento AS Procedimento,
        cobertura_convenio AS CoberturaConvenio, criado_em AS CriadoEm
        """;

    public async Task<Consulta> SalvarAsync(Consulta consulta, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var parametros = new
        {
            consulta.PacienteId,
            consulta.ProfissionalId,
            consulta.DataHora,
            Status = consulta.Status.ToString(),
            consulta.Procedimento,
            CoberturaConvenio = consulta.CoberturaConvenio?.ToString(),
            consulta.CriadoEm,
            consulta.Id
        };

        if (consulta.Id == 0)
        {
            const string insert = """
                INSERT INTO consulta
                    (paciente_id, profissional_id, data_hora, status, procedimento,
                     cobertura_convenio, criado_em)
                VALUES
                    (@PacienteId, @ProfissionalId, @DataHora, @Status, @Procedimento,
                     @CoberturaConvenio, @CriadoEm)
                RETURNING id
                """;

            consulta.Id = await conexao.ExecuteScalarAsync<long>(
                new CommandDefinition(insert, parametros, cancellationToken: ct));
            return consulta;
        }

        const string update = """
            UPDATE consulta
               SET paciente_id = @PacienteId, profissional_id = @ProfissionalId,
                   data_hora = @DataHora, status = @Status, procedimento = @Procedimento,
                   cobertura_convenio = @CoberturaConvenio
             WHERE id = @Id
            """;

        await conexao.ExecuteAsync(new CommandDefinition(update, parametros, cancellationToken: ct));
        return consulta;
    }

    public async Task<Consulta?> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"SELECT {Colunas} FROM consulta WHERE id = @id";
        var linha = await conexao.QueryFirstOrDefaultAsync<ConsultaRow>(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));

        return linha?.ParaDominio();
    }

    public async Task<IReadOnlyList<Consulta>> BuscarPorProfissionalEIntervaloAsync(
        long profissionalId, DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"""
            SELECT {Colunas} FROM consulta
             WHERE profissional_id = @profissionalId
               AND data_hora >= @inicio
               AND data_hora <  @fim
             ORDER BY data_hora
            """;

        var linhas = await conexao.QueryAsync<ConsultaRow>(
            new CommandDefinition(sql, new { profissionalId, inicio, fim }, cancellationToken: ct));

        return [.. linhas.Select(l => l.ParaDominio())];
    }

    public async Task<bool> ExisteConflitoAsync(
        long profissionalId, DateTime dataHora, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        const string sql = """
            SELECT COUNT(1) FROM consulta
             WHERE profissional_id = @profissionalId
               AND data_hora = @dataHora
               AND status <> 'Cancelada'
            """;

        var total = await conexao.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, new { profissionalId, dataHora }, cancellationToken: ct));

        return total > 0;
    }

    public async Task<IReadOnlyList<Consulta>> BuscarPorPacienteAsync(
        long pacienteId, CancellationToken ct = default)
    {
        await using var conexao = fabrica.Criar();

        var sql = $"""
            SELECT {Colunas} FROM consulta
             WHERE paciente_id = @pacienteId
             ORDER BY data_hora DESC
            """;

        var linhas = await conexao.QueryAsync<ConsultaRow>(
            new CommandDefinition(sql, new { pacienteId }, cancellationToken: ct));

        return [.. linhas.Select(l => l.ParaDominio())];
    }
}
