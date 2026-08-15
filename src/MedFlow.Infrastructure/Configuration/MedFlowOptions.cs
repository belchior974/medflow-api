namespace MedFlow.Infrastructure.Configuration;

/// <summary>Secao "MedFlow" do appsettings.json, tipada.</summary>
public sealed class MedFlowOptions
{
    public const string SecaoConfiguracao = "MedFlow";

    public AgendaOptions Agenda { get; init; } = new();
    public CacheOptions Cache { get; init; } = new();
    public ConvenioOptions Convenio { get; init; } = new();
    public BancoOptions Banco { get; init; } = new();

    public sealed class AgendaOptions
    {
        public int HoraInicio { get; init; } = 8;
        public int HoraFim { get; init; } = 18;
        public int DuracaoConsultaMinutos { get; init; } = 30;
        public int AntecedenciaMinimaHoras { get; init; } = 1;
    }

    public sealed class CacheOptions
    {
        /// <summary>L1 em memoria: TTL curto, local ao processo.</summary>
        public int L1TtlSegundos { get; init; } = 30;
        public long L1TamanhoMaximo { get; init; } = 10_000;

        /// <summary>L2 Redis: TTL maior, compartilhado entre instancias.</summary>
        public int L2TtlSegundos { get; init; } = 300;
    }

    public sealed class ConvenioOptions
    {
        public string BaseUrl { get; init; } = "http://localhost:8089";
        public int TimeoutMs { get; init; } = 2_000;

        // Equivalentes diretos da configuracao do Resilience4j no application.yml
        public int MaxTentativas { get; init; } = 3;
        public int DelayInicialMs { get; init; } = 300;
        public double LimiarDeFalha { get; init; } = 0.5;
        public int ChamadasMinimas { get; init; } = 5;
        public int JanelaAmostragemSegundos { get; init; } = 30;
        public int DuracaoCircuitoAbertoSegundos { get; init; } = 10;
        public int MaxChamadasConcorrentes { get; init; } = 20;
    }

    public sealed class BancoOptions
    {
        /// <summary>"sqlite" (padrao, roda sem infra) ou "postgres".</summary>
        public string Provider { get; init; } = "sqlite";
        public string ConnectionString { get; init; } = "Data Source=medflow.db";
        public bool AutoInicializarSchema { get; init; } = true;
    }
}
