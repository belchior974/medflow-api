using System.Data;
using System.Globalization;
using Dapper;

namespace MedFlow.Infrastructure.Persistence.Dapper;

/// <summary>
/// TRADUTORES DE TIPO DO DAPPER - necessarios por causa do SQLite.
/// <para>
/// O SQLite nao tem tipos de coluna reais, so classes de armazenamento. O
/// <c>Microsoft.Data.Sqlite</c> deriva o tipo do leitor da classe de armazenamento do
/// <b>valor</b>, e nao do tipo declarado da coluna: uma coluna declarada
/// <c>DATETIME</c> devolve <see cref="string"/> e uma <c>BOOLEAN</c> devolve
/// <see cref="long"/>. Mudar o DDL nao altera isso (verificado).
/// </para>
/// <para>
/// Sem estes handlers o Dapper nao consegue casar os construtores dos records de
/// <c>Rows.cs</c> - que usam <see cref="DateTime"/> e <see cref="bool"/> - e falha a
/// materializacao de toda linha lida, derrubando com HTTP 500 todos os endpoints
/// servidos por Dapper no perfil SQLite.
/// </para>
/// <para>
/// Sao registrados incondicionalmente e aceitam tambem os tipos que o Npgsql ja
/// devolve prontos (<see cref="DateTime"/> e <see cref="bool"/>), entao o caminho
/// PostgreSQL passa por eles sem mudanca de comportamento.
/// </para>
/// </summary>
internal static class DapperTypeHandlers
{
    private static int _registrados;

    public static void Registrar()
    {
        // Idempotente: o registro do Dapper e estatico e global ao processo.
        if (Interlocked.Exchange(ref _registrados, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateTimeHandler());
        SqlMapper.AddTypeHandler(new BoolHandler());
    }

    private sealed class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override DateTime Parse(object value) => value switch
        {
            DateTime pronto => pronto,
            string texto => DateTime.Parse(texto, CultureInfo.InvariantCulture),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };

        public override void SetValue(IDbDataParameter parameter, DateTime value) =>
            parameter.Value = value;
    }

    private sealed class BoolHandler : SqlMapper.TypeHandler<bool>
    {
        public override bool Parse(object value) => value switch
        {
            bool pronto => pronto,
            long inteiro => inteiro != 0,
            _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };

        public override void SetValue(IDbDataParameter parameter, bool value) =>
            parameter.Value = value;
    }
}
