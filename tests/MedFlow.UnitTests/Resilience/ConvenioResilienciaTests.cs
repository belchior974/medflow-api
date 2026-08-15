using MedFlow.Application.Ports;
using MedFlow.Domain.Model.Enums;
using MedFlow.Infrastructure.Configuration;
using MedFlow.Infrastructure.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Polly.CircuitBreaker;
using Polly.Registry;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace MedFlow.UnitTests.Resilience;

/// <summary>
/// BATERIA DE RESILIENCIA - o teste que sustenta a demonstracao do video.
/// <para>
/// Usa WireMock.Net para simular o servico externo de convenio em quatro cenarios:
/// </para>
/// <list type="number">
///   <item>resposta normal -&gt; Coberto;</item>
///   <item>indisponibilidade (HTTP 500) -&gt; fallback devolve Indeterminada;</item>
///   <item>falhas repetidas -&gt; o circuit breaker <b>abre</b>;</item>
///   <item>negativa de negocio (HTTP 422) -&gt; NaoCoberto e o circuito
///         <b>permanece fechado</b>.</item>
/// </list>
/// <para>Nao exige Docker nem banco.</para>
/// </summary>
public sealed class ConvenioResilienciaTests : IDisposable
{
    private readonly WireMockServer _convenioFalso = WireMockServer.Start();
    private readonly IMetricasPort _metricas = Substitute.For<IMetricasPort>();
    private readonly CircuitBreakerStateProvider _estadoDoCircuito = new();
    private readonly ServiceProvider _provider;
    private readonly ConvenioHttpGateway _gateway;

    public ConvenioResilienciaTests()
    {
        var opcoes = new MedFlowOptions.ConvenioOptions
        {
            BaseUrl = _convenioFalso.Url!,
            TimeoutMs = 1_000,
            MaxTentativas = 2,
            DelayInicialMs = 20,
            LimiarDeFalha = 0.5,
            ChamadasMinimas = 4,
            JanelaAmostragemSegundos = 30,
            DuracaoCircuitoAbertoSegundos = 10,
            MaxChamadasConcorrentes = 20
        };

        var servicos = new ServiceCollection();
        servicos.AddResiliencePipeline<string, StatusCobertura>(
            ConvenioHttpGateway.NomePipeline,
            (builder, _) => ConvenioPipelineFactory.Configurar(builder, opcoes, _estadoDoCircuito));

        _provider = servicos.BuildServiceProvider();

        var http = new HttpClient { BaseAddress = new Uri(_convenioFalso.Url!) };
        _gateway = new ConvenioHttpGateway(
            http,
            _provider.GetRequiredService<ResiliencePipelineProvider<string>>(),
            _metricas,
            NullLogger<ConvenioHttpGateway>.Instance);
    }

    private IRequestBuilder RotaDeCobertura() =>
        Request.Create().WithPath(new WireMock.Matchers.RegexMatcher(@"^/convenios/\d+/cobertura$")).UsingGet();

    [Fact(DisplayName = "convenio responde 200 com coberto=true -> Coberto")]
    public async Task CoberturaConfirmada()
    {
        _convenioFalso.Given(RotaDeCobertura()).RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody("""{"coberto":true,"motivo":"procedimento no rol"}"""));

        var resultado = await _gateway.VerificarCoberturaAsync(1, 1, "CONSULTA_CARDIOLOGIA");

        Assert.Equal(StatusCobertura.Coberto, resultado);
    }

    [Fact(DisplayName = "FALLBACK: convenio fora do ar (HTTP 500) -> Indeterminada, sem propagar erro")]
    public async Task FallbackQuandoServicoIndisponivel()
    {
        _convenioFalso.Given(RotaDeCobertura())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));

        var resultado = await _gateway.VerificarCoberturaAsync(1, 1, "CONSULTA_CARDIOLOGIA");

        // O ponto central: a API NAO estoura. Ela degrada.
        Assert.Equal(StatusCobertura.Indeterminada, resultado);
        _metricas.Received().FallbackConvenioAcionado();
    }

    [Fact(DisplayName = "CIRCUIT BREAKER: falhas repetidas abrem o circuito")]
    public async Task CircuitoAbreAposFalhasRepetidas()
    {
        _convenioFalso.Given(RotaDeCobertura())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));

        Assert.Equal(CircuitState.Closed, _estadoDoCircuito.CircuitState);

        for (var i = 0; i < 12; i++)
        {
            await _gateway.VerificarCoberturaAsync(1, 1, "CONSULTA");
        }

        Assert.NotEqual(CircuitState.Closed, _estadoDoCircuito.CircuitState);

        // Com o circuito aberto a chamada nem sai da aplicacao - responde na hora.
        var inicio = DateTime.UtcNow;
        var resultado = await _gateway.VerificarCoberturaAsync(1, 1, "CONSULTA");
        var duracao = DateTime.UtcNow - inicio;

        Assert.Equal(StatusCobertura.Indeterminada, resultado);
        Assert.True(duracao < TimeSpan.FromMilliseconds(500),
            $"com o circuito aberto a resposta deve ser imediata (foi {duracao.TotalMilliseconds}ms)");
    }

    [Fact(DisplayName = "NEGATIVA DE NEGOCIO (HTTP 422) -> NaoCoberto e o circuito segue FECHADO")]
    public async Task NegativaDeNegocioNaoAbreCircuito()
    {
        _convenioFalso.Given(RotaDeCobertura()).RespondWith(Response.Create()
            .WithStatusCode(422).WithBody("procedimento estetico fora do rol"));

        for (var i = 0; i < 12; i++)
        {
            var resultado = await _gateway.VerificarCoberturaAsync(1, 1, "PROCEDIMENTO_ESTETICO");
            Assert.Equal(StatusCobertura.NaoCoberto, resultado);
        }

        // Esta e a diferenca que separa falha de negocio de falha tecnica.
        Assert.Equal(CircuitState.Closed, _estadoDoCircuito.CircuitState);
        _metricas.DidNotReceive().FallbackConvenioAcionado();
    }

    public void Dispose()
    {
        _convenioFalso.Stop();
        _convenioFalso.Dispose();
        _provider.Dispose();
    }
}
