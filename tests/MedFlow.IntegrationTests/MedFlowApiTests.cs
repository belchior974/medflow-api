using System.Net;
using System.Net.Http.Json;
using MedFlow.Application.Dtos;
using MedFlow.Domain.Model;
using MedFlow.Domain.Model.Enums;
using StackExchange.Redis;
using Xunit;

namespace MedFlow.IntegrationTests;

/// <summary>
/// TESTE DE INTEGRACAO PONTA A PONTA com PostgreSQL e Redis reais.
/// <para>Percurso validado:</para>
/// <list type="number">
///   <item>cadastro via Dapper no Postgres real;</item>
///   <item>disponibilidade -&gt; MISS, calcula e popula L1+L2;</item>
///   <item>agendamento -&gt; grava, INVALIDA o cache e devolve 201;</item>
///   <item>disponibilidade de novo -&gt; o horario agendado sumiu (prova o evict);</item>
///   <item>prontuario -&gt; EF Core no mesmo Postgres + cache pelo decorador;</item>
///   <item>cancelamento -&gt; o horario volta a ficar livre;</item>
///   <item>metricas de negocio expostas em /metrics.</item>
/// </list>
/// <para>
/// Requer Docker. Sem Docker, o Testcontainers falha ao subir - rode
/// <c>dotnet test tests/MedFlow.UnitTests</c> para a suite que nao depende dele.
/// </para>
/// </summary>
[Collection("Integracao")]
public sealed class MedFlowApiTests(MedFlowFixture fixture) : IClassFixture<MedFlowFixture>
{
    private static DateTime ProximaSegundaAs9()
    {
        var data = DateTime.Today.AddDays(1);
        while (data.DayOfWeek != DayOfWeek.Monday)
        {
            data = data.AddDays(1);
        }

        return data.AddHours(9);
    }

    [Fact(DisplayName = "percurso completo: cadastro -> disponibilidade -> agendamento -> prontuario")]
    public async Task PercursoCompleto()
    {
        var cliente = fixture.CreateClient();
        var horario = ProximaSegundaAs9();
        var data = DateOnly.FromDateTime(horario);

        // ---------- 1. Cadastros (Dapper no Postgres real) ----------
        var profissional = await fixture.LerAsync<ProfissionalResponse>(
            await cliente.PostAsJsonAsync("/api/profissionais",
                new ProfissionalRequest("Dra. Ana Souza", "PEDIATRIA", $"CRM-SP-{Random.Shared.Next(100000, 999999)}")));

        var paciente = await fixture.LerAsync<PacienteResponse>(
            await cliente.PostAsJsonAsync("/api/pacientes",
                new PacienteRequest("Joana Silva", $"{Random.Shared.Next(100, 999)}.888.777-66",
                    new DateOnly(1992, 3, 20), "joana@exemplo.com", "17997770000", null)));

        // ---------- 2. Disponibilidade: MISS, calcula e cacheia ----------
        var inicial = await fixture.LerAsync<DisponibilidadeSnapshot>(
            await cliente.GetAsync($"/api/profissionais/{profissional.Id}/disponibilidade?data={data:yyyy-MM-dd}"));

        // Expediente 08h-18h em slots de 30min = 20 slots, todos livres.
        Assert.Equal(20, inicial.TotalSlots);
        Assert.Equal(20, inicial.HorariosLivres.Count);
        Assert.Contains("09:00", inicial.HorariosLivres);

        // A chave JSON deve existir no Redis REAL.
        await using var redis = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var chave = $"medflow:disponibilidade:{profissional.Id}:{data:yyyy-MM-dd}";
        Assert.True((await redis.GetDatabase().StringGetAsync(chave)).HasValue,
            "o snapshot deveria estar no L2 (Redis)");

        // ---------- 3. Agendamento (paciente particular -> sem chamada ao convenio) ----------
        var consulta = await fixture.LerAsync<ConsultaResponse>(
            await cliente.PostAsJsonAsync("/api/consultas",
                new AgendarConsultaRequest(paciente.Id, profissional.Id, horario, "CONSULTA_PEDIATRIA")));

        Assert.Equal(StatusConsulta.Agendada, consulta.Status);
        Assert.Equal(StatusCobertura.NaoAplicavel, consulta.CoberturaConvenio);

        // ---------- 4. O cache foi invalidado ----------
        var apos = await fixture.LerAsync<DisponibilidadeSnapshot>(
            await cliente.GetAsync($"/api/profissionais/{profissional.Id}/disponibilidade?data={data:yyyy-MM-dd}"));

        Assert.Equal(19, apos.HorariosLivres.Count);
        Assert.DoesNotContain("09:00", apos.HorariosLivres);

        // ---------- 5. Prontuario (EF Core) ----------
        var registro = await fixture.LerAsync<RegistroClinicoResponse>(
            await cliente.PostAsJsonAsync("/api/registros-clinicos",
                new RegistrarAtendimentoRequest(consulta.Id, "Faringite aguda",
                    "Amoxicilina 500mg 8/8h por 7 dias")));

        Assert.Equal(consulta.Id, registro.ConsultaId);

        // Busca dinamica com filtros combinados (composicao de IQueryable)
        var encontrados = await fixture.LerAsync<List<RegistroClinicoResponse>>(
            await cliente.GetAsync(
                $"/api/registros-clinicos/buscar?pacienteId={paciente.Id}&diagnostico=faringite"));

        Assert.Single(encontrados);

        // Resumo via projecao: primeira chamada popula o cache, segunda bate nele
        await cliente.GetAsync($"/api/registros-clinicos/paciente/{paciente.Id}/resumo");
        await cliente.GetAsync($"/api/registros-clinicos/paciente/{paciente.Id}/resumo");

        Assert.True(
            (await redis.GetDatabase().StringGetAsync($"medflow:historicoClinico:{paciente.Id}")).HasValue,
            "o resumo deveria ter sido gravado no L2 pelo decorador de cache");

        // ---------- 6. Cancelamento libera o slot ----------
        var cancelamento = await cliente.PatchAsync($"/api/consultas/{consulta.Id}/cancelar", null);
        Assert.Equal(HttpStatusCode.OK, cancelamento.StatusCode);

        var depoisDoCancelamento = await fixture.LerAsync<DisponibilidadeSnapshot>(
            await cliente.GetAsync($"/api/profissionais/{profissional.Id}/disponibilidade?data={data:yyyy-MM-dd}"));

        Assert.Contains("09:00", depoisDoCancelamento.HorariosLivres);

        // ---------- 7. Metricas de negocio ----------
        var metricas = await cliente.GetStringAsync("/metrics");
        Assert.Contains("medflow_consultas_criadas", metricas);
        Assert.Contains("medflow_cache_", metricas);
    }

    [Fact(DisplayName = "DEGRADACAO GRACIOSA: convenio inalcancavel nao impede o agendamento")]
    public async Task ConvenioForaDoArNaoImpedeAgendamento()
    {
        var cliente = fixture.CreateClient();
        var horario = ProximaSegundaAs9().AddHours(2);

        var profissional = await fixture.LerAsync<ProfissionalResponse>(
            await cliente.PostAsJsonAsync("/api/profissionais",
                new ProfissionalRequest("Dr. Paulo Reis", "CLINICA_GERAL", $"CRM-SP-{Random.Shared.Next(100000, 999999)}")));

        // Paciente COM convenio -> a verificacao externa e disparada.
        // O BaseUrl aponta para uma porta morta, entao o pipeline do Polly cai no fallback.
        var paciente = await fixture.LerAsync<PacienteResponse>(
            await cliente.PostAsJsonAsync("/api/pacientes",
                new PacienteRequest("Beatriz Alves", $"{Random.Shared.Next(100, 999)}.111.222-33",
                    new DateOnly(1988, 7, 9), null, null, 1)));

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new AgendarConsultaRequest(paciente.Id, profissional.Id, horario, "CONSULTA_GERAL"));

        // O ponto central: 201, nao 500.
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var consulta = await fixture.LerAsync<ConsultaResponse>(resposta);
        Assert.Equal(StatusConsulta.AguardandoValidacaoConvenio, consulta.Status);
        Assert.Equal(StatusCobertura.Indeterminada, consulta.CoberturaConvenio);
    }

    [Fact(DisplayName = "regra de negocio violada devolve 422 com ProblemDetails")]
    public async Task RegraVioladaDevolve422()
    {
        var cliente = fixture.CreateClient();

        var domingo = DateTime.Today.AddDays(1);
        while (domingo.DayOfWeek != DayOfWeek.Sunday)
        {
            domingo = domingo.AddDays(1);
        }

        var resposta = await cliente.PostAsJsonAsync("/api/consultas",
            new AgendarConsultaRequest(1, 1, domingo.AddHours(10), "CONSULTA"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("HORARIO_COMERCIAL", corpo);
        Assert.Contains("traceId", corpo);
    }

    [Fact(DisplayName = "payload invalido devolve 400 com os erros de validacao")]
    public async Task PayloadInvalidoDevolve400()
    {
        var cliente = fixture.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/pacientes",
            new PacienteRequest("", "123", new DateOnly(1990, 1, 1), "nao-e-email", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("email", await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
