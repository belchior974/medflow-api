using MedFlow.Application.Dtos;
using MedFlow.Application.Ports;
using MedFlow.Application.UseCases;
using MedFlow.Domain.Exceptions;
using MedFlow.Domain.Gateways;
using MedFlow.Domain.Model;
using MedFlow.Domain.Model.Enums;
using MedFlow.Domain.Repositories;
using MedFlow.Domain.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MedFlow.UnitTests.UseCases;

/// <summary>
/// Testes unitarios do use case central.
/// <para>
/// TODOS os colaboradores sao substitutos de <b>interfaces</b>. Nao ha banco, nao ha HTTP
/// e nao ha container de DI: e exatamente esse o beneficio pratico do DIP - o nucleo de
/// negocio e testavel isoladamente e em milissegundos.
/// </para>
/// </summary>
public sealed class AgendarConsultaUseCaseTests
{
    private static readonly ParametrosAgenda Parametros = new(8, 18, 30, 1);

    private readonly IConsultaRepository _consultas = Substitute.For<IConsultaRepository>();
    private readonly IPacienteRepository _pacientes = Substitute.For<IPacienteRepository>();
    private readonly IConvenioGateway _convenio = Substitute.For<IConvenioGateway>();
    private readonly IDisponibilidadeEventPublisher _eventos = Substitute.For<IDisponibilidadeEventPublisher>();
    private readonly IDisponibilidadeCache _cache = Substitute.For<IDisponibilidadeCache>();
    private readonly IMetricasPort _metricas = Substitute.For<IMetricasPort>();

    private readonly AgendarConsultaUseCase _useCase;

    public AgendarConsultaUseCaseTests()
    {
        _consultas.SalvarAsync(Arg.Any<Consulta>(), Arg.Any<CancellationToken>())
            .Returns(chamada =>
            {
                var consulta = chamada.Arg<Consulta>();
                consulta.Id = 99;
                return Task.FromResult(consulta);
            });

        _useCase = new AgendarConsultaUseCase(
            _consultas, _pacientes, _convenio, _eventos, _cache, _metricas,
            [new RegraHorarioComercial(Parametros)],
            TimeProvider.System,
            NullLogger<AgendarConsultaUseCase>.Instance);
    }

    /// <summary>Proxima segunda as 09:00 - sempre valida perante as regras.</summary>
    private static DateTime ProximaSegundaAs9()
    {
        var data = DateTime.Today.AddDays(1);
        while (data.DayOfWeek != DayOfWeek.Monday)
        {
            data = data.AddDays(1);
        }

        return data.AddHours(9);
    }

    private static Paciente PacienteComConvenio() => new()
    {
        Id = 1, Nome = "Carlos", Cpf = "111.222.333-44",
        DataNascimento = new DateOnly(1990, 5, 14), ConvenioId = 1
    };

    private static Paciente PacienteParticular() => new()
    {
        Id = 2, Nome = "Marina", Cpf = "555.666.777-88",
        DataNascimento = new DateOnly(1985, 11, 2), ConvenioId = null
    };

    [Fact(DisplayName = "agenda com status Agendada quando o convenio confirma a cobertura")]
    public async Task AgendaComCoberturaConfirmada()
    {
        _pacientes.BuscarPorIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(PacienteComConvenio()));
        _convenio.VerificarCoberturaAsync(1, 1, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(StatusCobertura.Coberto));

        var request = new AgendarConsultaRequest(1, 10, ProximaSegundaAs9(), "CONSULTA_CARDIOLOGIA");

        var resposta = await _useCase.ExecutarAsync(request);

        Assert.Equal(StatusConsulta.Agendada, resposta.Status);
        Assert.Equal(StatusCobertura.Coberto, resposta.CoberturaConvenio);
        Assert.Equal(99, resposta.Id);
        _metricas.Received(1).ConsultaCriada(StatusCobertura.Coberto);
    }

    [Fact(DisplayName = "DEGRADACAO GRACIOSA: convenio indisponivel nao derruba o agendamento")]
    public async Task AgendaEmModoDegradadoQuandoConvenioIndisponivel()
    {
        _pacientes.BuscarPorIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(PacienteComConvenio()));
        // E o que o fallback do circuit breaker devolve quando o servico externo cai.
        _convenio.VerificarCoberturaAsync(1, 1, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(StatusCobertura.Indeterminada));

        var request = new AgendarConsultaRequest(1, 10, ProximaSegundaAs9(), "CONSULTA_CARDIOLOGIA");

        var resposta = await _useCase.ExecutarAsync(request);

        Assert.Equal(StatusConsulta.AguardandoValidacaoConvenio, resposta.Status);
        Assert.Equal(StatusCobertura.Indeterminada, resposta.CoberturaConvenio);
    }

    [Fact(DisplayName = "paciente particular nao aciona o servico de convenio")]
    public async Task PacienteParticularNaoChamaConvenio()
    {
        _pacientes.BuscarPorIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(PacienteParticular()));

        var request = new AgendarConsultaRequest(2, 10, ProximaSegundaAs9(), "CONSULTA_ORTOPEDIA");

        var resposta = await _useCase.ExecutarAsync(request);

        Assert.Equal(StatusCobertura.NaoAplicavel, resposta.CoberturaConvenio);
        await _convenio.DidNotReceive().VerificarCoberturaAsync(
            Arg.Any<long>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "rejeita agendamento fora do horario comercial e registra a metrica da regra")]
    public async Task RejeitaForaDoHorarioComercial()
    {
        _pacientes.BuscarPorIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(PacienteComConvenio()));

        var domingo = DateTime.Today.AddDays(1);
        while (domingo.DayOfWeek != DayOfWeek.Sunday)
        {
            domingo = domingo.AddDays(1);
        }

        var request = new AgendarConsultaRequest(1, 10, domingo.AddHours(10), "CONSULTA_CARDIOLOGIA");

        var erro = await Assert.ThrowsAsync<RegraDeAgendamentoVioladaException>(
            () => _useCase.ExecutarAsync(request));

        Assert.Equal("HORARIO_COMERCIAL", erro.Regra);
        await _consultas.DidNotReceive().SalvarAsync(Arg.Any<Consulta>(), Arg.Any<CancellationToken>());
        _metricas.Received(1).RegraDeAgendamentoViolada("HORARIO_COMERCIAL");
    }

    [Fact(DisplayName = "falha quando o paciente nao existe")]
    public async Task FalhaQuandoPacienteInexistente()
    {
        _pacientes.BuscarPorIdAsync(404, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(null));

        var request = new AgendarConsultaRequest(404, 10, ProximaSegundaAs9(), "CONSULTA");

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _useCase.ExecutarAsync(request));
    }

    [Fact(DisplayName = "OCP: uma regra nova entra so por injecao, sem alterar o use case")]
    public async Task RegraNovaEntraPorInjecao()
    {
        _pacientes.BuscarPorIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Paciente?>(PacienteComConvenio()));

        var useCaseComRegraExtra = new AgendarConsultaUseCase(
            _consultas, _pacientes, _convenio, _eventos, _cache, _metricas,
            [new RegraHorarioComercial(Parametros), new RegraQueSempreRecusa()],
            TimeProvider.System,
            NullLogger<AgendarConsultaUseCase>.Instance);

        var request = new AgendarConsultaRequest(1, 10, ProximaSegundaAs9(), "CONSULTA");

        var erro = await Assert.ThrowsAsync<RegraDeAgendamentoVioladaException>(
            () => useCaseComRegraExtra.ExecutarAsync(request));

        Assert.Equal("REGRA_DE_TESTE", erro.Regra);
    }

    /// <summary>Regra criada apenas no teste - prova que estender nao exige recompilar o use case.</summary>
    private sealed class RegraQueSempreRecusa : IRegraAgendamento
    {
        public string Nome => "REGRA_DE_TESTE";
        public int Ordem => 999;

        public Task ValidarAsync(Consulta consulta, CancellationToken ct = default) =>
            throw new RegraDeAgendamentoVioladaException(Nome, "recusa proposital");
    }
}
