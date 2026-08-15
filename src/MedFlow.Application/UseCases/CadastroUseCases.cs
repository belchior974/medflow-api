using MedFlow.Application.Dtos;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Application.UseCases;

/// <summary>Cadastro de pacientes - CRUD de apoio.</summary>
public sealed class CadastrarPacienteUseCase(IPacienteRepository repositorio)
{
    public async Task<PacienteResponse> ExecutarAsync(
        PacienteRequest request, CancellationToken ct = default)
    {
        var paciente = new Paciente
        {
            Nome = request.Nome,
            Cpf = request.Cpf,
            DataNascimento = request.DataNascimento,
            Email = request.Email,
            Telefone = request.Telefone,
            ConvenioId = request.ConvenioId
        };

        return PacienteResponse.De(await repositorio.SalvarAsync(paciente, ct));
    }

    public async Task<PacienteResponse> BuscarPorIdAsync(long id, CancellationToken ct = default)
    {
        var paciente = await repositorio.BuscarPorIdAsync(id, ct)
            ?? throw new RecursoNaoEncontradoException("Paciente", id);
        return PacienteResponse.De(paciente);
    }

    public async Task<IReadOnlyList<PacienteResponse>> ListarAsync(CancellationToken ct = default)
    {
        var pacientes = await repositorio.ListarTodosAsync(ct);
        return [.. pacientes.Select(PacienteResponse.De)];
    }
}

/// <summary>Cadastro de profissionais - CRUD de apoio.</summary>
public sealed class CadastrarProfissionalUseCase(IProfissionalRepository repositorio)
{
    public async Task<ProfissionalResponse> ExecutarAsync(
        ProfissionalRequest request, CancellationToken ct = default)
    {
        var profissional = new Profissional
        {
            Nome = request.Nome,
            Especialidade = request.Especialidade,
            Crm = request.Crm,
            Ativo = true
        };

        return ProfissionalResponse.De(await repositorio.SalvarAsync(profissional, ct));
    }

    public async Task<IReadOnlyList<ProfissionalResponse>> ListarAsync(CancellationToken ct = default)
    {
        var profissionais = await repositorio.ListarTodosAsync(ct);
        return [.. profissionais.Select(ProfissionalResponse.De)];
    }
}
