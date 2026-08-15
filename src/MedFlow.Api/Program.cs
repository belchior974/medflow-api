using MedFlow.Api;
using MedFlow.Api.Endpoints;
using MedFlow.Application;
using MedFlow.Infrastructure;
using MedFlow.Infrastructure.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly.CircuitBreaker;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Camadas (a ordem espelha a direcao das dependencias)
// ---------------------------------------------------------------------------
builder.Services.AddMedFlowApplication();
builder.Services.AddMedFlowInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// OBSERVABILIDADE - os tres pilares, via OpenTelemetry
//   metricas -> /metrics no formato Prometheus
//   traces   -> Zipkin
//   logs     -> console com TraceId correlacionado
// ---------------------------------------------------------------------------
var zipkin = builder.Configuration["OpenTelemetry:ZipkinEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(recurso => recurso.AddService(
        serviceName: "medflow-api",
        serviceVersion: "1.0.0"))
    .WithMetrics(metricas => metricas
        .AddMeter(MedFlowMetrics.NomeMeter)          // metricas de NEGOCIO
        .AddAspNetCoreInstrumentation()               // latencia/throughput HTTP
        .AddHttpClientInstrumentation()               // chamadas ao convenio
        .AddRuntimeInstrumentation()                  // GC, threads, alocacao
        .AddPrometheusExporter())
    .WithTracing(traces =>
    {
        traces.AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(zipkin))
        {
            traces.AddZipkinExporter(o => o.Endpoint = new Uri(zipkin));
        }
    });

builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// ---------------------------------------------------------------------------
// Erros, OpenAPI e health checks
// ---------------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

// ---------------------------------------------------------------------------
// ROTEAMENTO - equivalente ao RouterConfig funcional do Modulo 3.
// Assim como la, este bloco declara APENAS o mapa de rotas: nao valida
// (filtro), nao converte (DTO), nao consulta banco (use case) e nao trata
// erro (GlobalExceptionHandler). SRP levado a serio.
// ---------------------------------------------------------------------------
var api = app.MapGroup("/api");
api.MapearCadastros()
   .MapearDisponibilidade()
   .MapearConsultas()
   .MapearProntuario();

// ---------------------------------------------------------------------------
// Observabilidade exposta
// ---------------------------------------------------------------------------
app.MapPrometheusScrapingEndpoint();      // GET /metrics
app.MapHealthChecks("/health");

/// Equivalente ao /actuator/circuitbreakers do Spring Boot.
app.MapGet("/health/circuit", (CircuitBreakerStateProvider estado) => Results.Ok(new
{
    pipeline = "convenio-service",
    circuitoAberto = estado.CircuitState is CircuitState.Open or CircuitState.Isolated,
    estado = estado.CircuitState.ToString()
})).WithTags("Observabilidade").WithName("EstadoDoCircuito");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ExporOpenApi"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(opcoes => opcoes.WithTitle("MedFlow API"));
}

app.Run();

/// <summary>Exposto para o WebApplicationFactory dos testes de integracao.</summary>
public partial class Program;
