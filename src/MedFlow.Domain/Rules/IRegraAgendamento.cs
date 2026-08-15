using MedFlow.Domain.Model;

namespace MedFlow.Domain.Rules;

/// <summary>
/// OCP na pratica: novas regras de agendamento sao adicionadas criando uma nova
/// implementacao desta interface e registrando-a no container. O
/// <c>AgendarConsultaUseCase</c> recebe <c>IEnumerable&lt;IRegraAgendamento&gt;</c> por
/// injecao e <b>nunca precisa ser alterado</b>.
/// </summary>
public interface IRegraAgendamento
{
    /// <summary>Nome da regra, usado na mensagem de erro e nas metricas.</summary>
    string Nome { get; }

    /// <summary>Ordem de execucao: regras mais baratas primeiro.</summary>
    int Ordem => 100;

    /// <summary>Deve retornar normalmente se valido, ou lancar excecao de dominio se violado.</summary>
    Task ValidarAsync(Consulta consulta, CancellationToken ct = default);
}
