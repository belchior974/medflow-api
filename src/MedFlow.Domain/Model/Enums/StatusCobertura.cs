namespace MedFlow.Domain.Model.Enums;

/// <summary>Resultado da verificacao junto ao servico externo de convenio.</summary>
public enum StatusCobertura
{
    /// <summary>O convenio confirmou a cobertura do procedimento.</summary>
    Coberto,

    /// <summary>O convenio respondeu e NEGOU a cobertura (regra de negocio, nao falha tecnica).</summary>
    NaoCoberto,

    /// <summary>Nao foi possivel falar com o convenio (timeout / circuito aberto): fallback acionado.</summary>
    Indeterminada,

    /// <summary>Paciente particular: nao ha convenio a consultar.</summary>
    NaoAplicavel
}
