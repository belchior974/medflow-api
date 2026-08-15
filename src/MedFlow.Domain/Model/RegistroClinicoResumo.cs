namespace MedFlow.Domain.Model;

/// <summary>
/// Resultado da <b>projecao</b> do prontuario: apenas as colunas necessarias para o
/// resumo, evitando carregar <c>Prescricao</c> (varchar 1000) e o restante do agregado.
/// Reduz I/O de banco e o payload em cache.
/// </summary>
public sealed record RegistroClinicoResumo(
    long Id,
    long ConsultaId,
    string Diagnostico,
    DateTime DataRegistro);
