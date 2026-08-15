namespace MedFlow.Domain.Exceptions;

/// <summary>Raiz das excecoes de negocio. Mapeada para 4xx no handler global.</summary>
public abstract class DomainException(string message) : Exception(message);
