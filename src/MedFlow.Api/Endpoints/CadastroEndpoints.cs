using MedFlow.Api.Filters;
using MedFlow.Application.Dtos;
using MedFlow.Application.UseCases;

namespace MedFlow.Api.Endpoints;

public static class CadastroEndpoints
{
    public static RouteGroupBuilder MapearCadastros(this RouteGroupBuilder api)
    {
        var pacientes = api.MapGroup("/pacientes").WithTags("Pacientes");

        pacientes.MapPost("/", async (
                PacienteRequest request, CadastrarPacienteUseCase useCase, CancellationToken ct) =>
            {
                var criado = await useCase.ExecutarAsync(request, ct);
                return Results.Created($"/api/pacientes/{criado.Id}", criado);
            })
            .ComValidacao<PacienteRequest>()
            .WithName("CadastrarPaciente")
            .WithSummary("Cadastra um paciente");

        pacientes.MapGet("/{id:long}", async (
                long id, CadastrarPacienteUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.BuscarPorIdAsync(id, ct)))
            .WithName("BuscarPaciente");

        pacientes.MapGet("/", async (CadastrarPacienteUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.ListarAsync(ct)))
            .WithName("ListarPacientes");

        var profissionais = api.MapGroup("/profissionais").WithTags("Profissionais");

        profissionais.MapPost("/", async (
                ProfissionalRequest request, CadastrarProfissionalUseCase useCase, CancellationToken ct) =>
            {
                var criado = await useCase.ExecutarAsync(request, ct);
                return Results.Created($"/api/profissionais/{criado.Id}", criado);
            })
            .ComValidacao<ProfissionalRequest>()
            .WithName("CadastrarProfissional");

        profissionais.MapGet("/", async (CadastrarProfissionalUseCase useCase, CancellationToken ct) =>
                Results.Ok(await useCase.ListarAsync(ct)))
            .WithName("ListarProfissionais");

        return api;
    }
}
