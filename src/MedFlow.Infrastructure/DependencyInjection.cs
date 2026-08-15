using MedFlow.Application.Ports;
using MedFlow.Domain.Gateways;
using MedFlow.Domain.Model.Enums;
using MedFlow.Domain.Repositories;
using MedFlow.Domain.Rules;
using MedFlow.Infrastructure.Cache;
using MedFlow.Infrastructure.Configuration;
using MedFlow.Infrastructure.Events;
using MedFlow.Infrastructure.Gateway;
using MedFlow.Infrastructure.Metrics;
using MedFlow.Infrastructure.Persistence;
using MedFlow.Infrastructure.Persistence.Dapper;
using MedFlow.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;

namespace MedFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMedFlowInfrastructure(
        this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<MedFlowOptions>(
            configuracao.GetSection(MedFlowOptions.SecaoConfiguracao));

        services.AddSingleton(TimeProvider.System);

        AdicionarDominio(services);
        AdicionarPersistencia(services);
        AdicionarCache(services, configuracao);
        AdicionarObservabilidade(services);
        AdicionarResiliencia(services);

        services.AddHostedService<DatabaseInitializer>();
        return services;
    }

    // -----------------------------------------------------------------------
    // Dominio: parametros e regras
    // -----------------------------------------------------------------------
    private static void AdicionarDominio(IServiceCollection services)
    {
        // ParametrosAgenda e um tipo do DOMINIO; a infraestrutura so o alimenta
        // com os valores do appsettings.json. As regras nao conhecem IOptions.
        services.AddSingleton(sp =>
        {
            var agenda = sp.GetRequiredService<IOptions<MedFlowOptions>>().Value.Agenda;
            return new ParametrosAgenda(
                agenda.HoraInicio,
                agenda.HoraFim,
                agenda.DuracaoConsultaMinutos,
                agenda.AntecedenciaMinimaHoras);
        });

        // OCP: para adicionar "paciente nao pode ter duas consultas no mesmo dia",
        // basta criar a classe e acrescentar UMA linha aqui.
        // AgendarConsultaUseCase recebe IEnumerable<IRegraAgendamento> e nao muda.
        services.AddScoped<IRegraAgendamento, RegraHorarioComercial>();
        services.AddScoped<IRegraAgendamento, RegraAntecedenciaMinima>();
        services.AddScoped<IRegraAgendamento, RegraProfissionalDisponivel>();
        services.AddScoped<IRegraAgendamento, RegraSemConflitoDeHorario>();
    }

    // -----------------------------------------------------------------------
    // Persistencia: Dapper (agendamento) + EF Core (prontuario)
    // -----------------------------------------------------------------------
    private static void AdicionarPersistencia(IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        services.AddScoped<IConsultaRepository, ConsultaDapperRepository>();
        services.AddScoped<IPacienteRepository, PacienteDapperRepository>();
        services.AddScoped<IProfissionalRepository, ProfissionalDapperRepository>();

        services.AddDbContext<MedFlowDbContext>((sp, opcoes) =>
        {
            var banco = sp.GetRequiredService<IOptions<MedFlowOptions>>().Value.Banco;
            if (banco.Provider.ToLowerInvariant() is "postgres" or "postgresql")
            {
                opcoes.UseNpgsql(banco.ConnectionString);
            }
            else
            {
                opcoes.UseSqlite(banco.ConnectionString);
            }
        });

        // DECORADOR: quem pede IRegistroClinicoRepository recebe a versao com cache,
        // que por sua vez embrulha a versao pura de EF Core. Nenhum use case sabe disso.
        services.AddScoped<RegistroClinicoRepository>();
        services.AddScoped<IRegistroClinicoRepository, CachedRegistroClinicoRepository>();
    }

    // -----------------------------------------------------------------------
    // Cache multinivel
    // -----------------------------------------------------------------------
    private static void AdicionarCache(IServiceCollection services, IConfiguration configuracao)
    {
        services.AddMemoryCache(opcoes => opcoes.SizeLimit = 10_000);

        var redisConexao = configuracao.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConexao))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var opcoes = ConfigurationOptions.Parse(redisConexao);
                // CRUCIAL para a degradacao graciosa: sem isso a aplicacao NAO SOBE
                // quando o Redis esta fora do ar.
                opcoes.AbortOnConnectFail = false;
                opcoes.ConnectTimeout = 2_000;
                opcoes.SyncTimeout = 2_000;
                return ConnectionMultiplexer.Connect(opcoes);
            });
        }

        // Fabrica explicita: o Redis e OPCIONAL (pode nem estar registrado).
        services.AddSingleton<ICachePort>(sp => new TwoLevelCache(
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<IMetricasPort>(),
            sp.GetRequiredService<IOptions<MedFlowOptions>>(),
            sp.GetRequiredService<ILogger<TwoLevelCache>>(),
            sp.GetService<IConnectionMultiplexer>()));
    }

    private static void AdicionarObservabilidade(IServiceCollection services)
    {
        services.AddSingleton<IMetricasPort, MedFlowMetrics>();
        services.AddSingleton<IDisponibilidadeEventPublisher, DisponibilidadeEventPublisher>();
    }

    // -----------------------------------------------------------------------
    // Resiliencia (Polly v8) + HttpClient do convenio
    // -----------------------------------------------------------------------
    private static void AdicionarResiliencia(IServiceCollection services)
    {
        // Exposto como singleton para que o endpoint /health/circuit possa consultar
        // o estado do circuito - equivalente ao /actuator/circuitbreakers do Spring.
        services.AddSingleton<CircuitBreakerStateProvider>();

        services.AddResiliencePipeline<string, StatusCobertura>(
            ConvenioHttpGateway.NomePipeline,
            (builder, contexto) =>
            {
                var opcoes = contexto.ServiceProvider
                    .GetRequiredService<IOptions<MedFlowOptions>>().Value.Convenio;
                var estado = contexto.ServiceProvider
                    .GetRequiredService<CircuitBreakerStateProvider>();

                ConvenioPipelineFactory.Configurar(builder, opcoes, estado);
            });

        services.AddHttpClient<IConvenioGateway, ConvenioHttpGateway>((sp, cliente) =>
        {
            var opcoes = sp.GetRequiredService<IOptions<MedFlowOptions>>().Value.Convenio;
            cliente.BaseAddress = new Uri(opcoes.BaseUrl);
            // Timeout do socket generoso: quem corta de verdade e o Timeout do pipeline.
            cliente.Timeout = TimeSpan.FromMilliseconds(opcoes.TimeoutMs * 3);
        });
    }
}
