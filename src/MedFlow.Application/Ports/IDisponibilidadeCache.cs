namespace MedFlow.Application.Ports;

/// <summary>
/// ISP na pratica: quem agenda ou cancela uma consulta precisa apenas <b>invalidar</b> a
/// agenda daquele dia - nao precisa saber consultar disponibilidade.
/// <para>
/// Sem esta interface, <c>AgendarConsultaUseCase</c> dependeria da classe concreta
/// <c>ConsultarDisponibilidadeUseCase</c> inteira: acoplamento maior do que o necessario
/// e, de quebra, um colaborador impossivel de substituir em teste.
/// </para>
/// </summary>
public interface IDisponibilidadeCache
{
    ValueTask InvalidarAsync(long profissionalId, DateOnly data, CancellationToken ct = default);
}
