namespace MedFlow.Domain.Exceptions;

public sealed class RecursoNaoEncontradoException(string recurso, object id)
    : DomainException($"{recurso} nao encontrado(a) para o identificador {id}")
{
    public string Recurso { get; } = recurso;
    public object Id { get; } = id;
}
