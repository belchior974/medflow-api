using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace MedFlow.IntegrationTests;

/// <summary>
/// Sobe PostgreSQL e Redis <b>reais</b> em Docker (Testcontainers) e liga a aplicacao
/// neles via <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// <para>
/// Nada de banco em memoria fingindo ser Postgres nem de cache falso: o mesmo caminho de
/// codigo que roda em producao e exercitado aqui, inclusive o driver Npgsql e a
/// serializacao real no Redis.
/// </para>
/// </summary>
public sealed class MedFlowFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("medflow")
        .WithUsername("medflow")
        .WithPassword("medflow")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MedFlow:Banco:Provider"] = "postgres",
                ["MedFlow:Banco:ConnectionString"] = _postgres.GetConnectionString(),
                ["MedFlow:Banco:AutoInicializarSchema"] = "true",
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),

                // Sem servico de convenio de pe: o fallback deve assumir sem quebrar nada.
                ["MedFlow:Convenio:BaseUrl"] = "http://localhost:59999",
                ["MedFlow:Convenio:TimeoutMs"] = "300",
                ["MedFlow:Convenio:MaxTentativas"] = "1",

                // Sem Zipkin no CI.
                ["OpenTelemetry:ZipkinEndpoint"] = ""
            });
        });
    }

    public async Task<T> LerAsync<T>(HttpResponseMessage resposta)
    {
        resposta.EnsureSuccessStatusCode();
        var corpo = await resposta.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(corpo);
        return corpo;
    }
}
