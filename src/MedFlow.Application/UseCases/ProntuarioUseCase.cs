using MedFlow.Application.Dtos;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Model;
using MedFlow.Domain.Repositories;

namespace MedFlow.Application.UseCases;

/// <summary>
/// Prontuario eletronico: busca dinamica e projecao sobre o EF Core.
/// <para>
/// Diferenca notavel em relacao a versao Spring: la, o acesso JPA era bloqueante e
/// precisava ser empurrado para um <c>Schedulers.boundedElastic()</c> para nao travar a
/// event loop do Netty. No .NET, o EF Core ja expoe I/O assincrono de ponta a ponta -
/// nao existe caminho bloqueante a isolar, e o codigo fica mais simples por consequencia.
/// </para>
/// </summary>
public sealed class ProntuarioUseCase(
    IRegistroClinicoRepository registroRepository,
    IConsultaRepository consultaRepository,
    TimeProvider tempo)
{
    public async Task<RegistroClinicoResponse> RegistrarAtendimentoAsync(
        RegistrarAtendimentoRequest request, CancellationToken ct = default)
    {
        var consulta = await consultaRepository.BuscarPorIdAsync(request.ConsultaId, ct)
            ?? throw new RecursoNaoEncontradoException("Consulta", request.ConsultaId);

        var registro = new RegistroClinico
        {
            ConsultaId = consulta.Id,
            PacienteId = consulta.PacienteId,
            ProfissionalId = consulta.ProfissionalId,
            Diagnostico = request.Diagnostico,
            Prescricao = request.Prescricao,
            DataRegistro = tempo.GetLocalNow().DateTime
        };

        var salvo = await registroRepository.SalvarAsync(registro, ct);
        return RegistroClinicoResponse.De(salvo);
    }

    /// <summary>Busca dinamica: o filtro vira composicao de IQueryable no repositorio.</summary>
    public async Task<IReadOnlyList<RegistroClinicoResponse>> BuscarAsync(
        FiltroHistoricoClinico filtro, CancellationToken ct = default)
    {
        var registros = await registroRepository.BuscarPorFiltroAsync(filtro, ct);
        return [.. registros.Select(RegistroClinicoResponse.De)];
    }

    /// <summary>Resumo via projecao + cache no repositorio decorado.</summary>
    public Task<IReadOnlyList<RegistroClinicoResumo>> ResumoPorPacienteAsync(
        long pacienteId, CancellationToken ct = default) =>
        registroRepository.ResumoPorPacienteAsync(pacienteId, ct);
}
