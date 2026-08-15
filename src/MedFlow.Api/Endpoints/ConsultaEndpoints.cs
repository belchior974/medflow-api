using MedFlow.Api.Filters;
using MedFlow.Application.Dtos;
using MedFlow.Application.UseCases;

namespace MedFlow.Api.Endpoints;

public static class ConsultaEndpoints
{
    public static RouteGroupBuilder MapearConsultas(this RouteGroupBuilder api)
    {
        var consultas = api.MapGroup("/consultas").WithTags("Consultas");

        consultas.MapPost("/", async (
                AgendarConsultaRequest request, AgendarConsultaUseCase useCase, CancellationToken ct) =>
            {
                var criada = await useCase.ExecutarAsync(request, ct);
                return Results.Created($"/api/consultas/{criada.Id}", criada);
            })
            .ComValidacao<AgendarConsultaRequest>()
            .WithName("AgendarConsulta")
            .WithSummary("Agenda uma consulta")
            .WithDescription(
                "Dispara a verificacao de cobertura no servico externo de convenio, protegida "
                + "por circuit breaker. Se o convenio estiver fora do ar, a consulta e criada "
                + "assim mesmo com status AguardandoValidacaoConvenio (degradacao graciosa).");

        consultas.MapGet("/{id:long}", async (
                long id, GerenciarConsultaUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.BuscarPorIdAsync(id, ct)))
            .WithName("BuscarConsulta");

        consultas.MapPatch("/{id:long}/cancelar", async (
                long id, GerenciarConsultaUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.CancelarAsync(id, ct)))
            .WithName("CancelarConsulta")
            .WithSummary("Cancela a consulta, invalida o cache e emite evento SSE");

        consultas.MapPatch("/{id:long}/confirmar", async (
                long id, GerenciarConsultaUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.ConfirmarAsync(id, ct)))
            .WithName("ConfirmarConsulta");

        api.MapGet("/pacientes/{pacienteId:long}/consultas", async (
                long pacienteId, GerenciarConsultaUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.ListarPorPacienteAsync(pacienteId, ct)))
            .WithTags("Consultas")
            .WithName("ListarConsultasDoPaciente");

        return api;
    }
}
