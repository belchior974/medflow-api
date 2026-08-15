namespace MedFlow.Domain.Exceptions;

/// <summary>
/// O convenio RESPONDEU e negou a cobertura.
/// <para>
/// Importante para a resiliencia: esta e uma <b>falha de negocio esperada</b>, nao uma
/// falha tecnica. Por isso o pipeline do Polly a trata como resultado valido - um convenio
/// negando cobertura 100 vezes seguidas nao pode abrir o circuito nem disparar retentativas.
/// </para>
/// </summary>
public sealed class CoberturaNegadaException(string motivo) : DomainException(motivo);
