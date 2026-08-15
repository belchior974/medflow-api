using MedFlow.Api.Filters;
using MedFlow.Application.Dtos;
using MedFlow.Application.UseCases;
using MedFlow.Domain.Model;

namespace MedFlow.Api.Endpoints;

public static class ProntuarioEndpoints
{
    public static RouteGroupBuilder MapearProntuario(this RouteGroupBuilder api)
    {
        var grupo = api.MapGroup("/registros-clinicos").WithTags("Prontuario");

        grupo.MapPost("/", async (
                RegistrarAtendimentoRequest request, ProntuarioUseCase useCase, CancellationToken ct) =>
            {
                var criado = await useCase.RegistrarAtendimentoAsync(request, ct);
                return Results.Created($"/api/registros-clinicos/{criado.Id}", criado);
            })
            .ComValidacao<RegistrarAtendimentoRequest>()
            .WithName("RegistrarAtendimento");

        grupo.MapGet("/buscar", async (
                long? pacienteId,
                long? profissionalId,
                DateTime? periodoInicio,
                DateTime? periodoFim,
                string? diagnostico,
                ProntuarioUseCase useCase,
                CancellationToken ct) =>
            {
                var filtro = new FiltroHistoricoClinico(
                    pacienteId, profissionalId, periodoInicio, periodoFim, diagnostico);
                return Results.Ok(await useCase.BuscarAsync(filtro, ct));
            })
            .WithName("BuscarHistorico")
            .WithSummary("Busca dinamica no historico")
            .WithDescription("Todos os filtros sao opcionais e combinaveis; os nulos nao entram no WHERE.");

        grupo.MapGet("/paciente/{pacienteId:long}/resumo", async (
                long pacienteId, ProntuarioUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.ResumoPorPacienteAsync(pacienteId, ct)))
            .WithName("ResumoDoPaciente")
            .WithSummary("Resumo via projecao - servido pelo cache L1/L2");

        return api;
    }
}
