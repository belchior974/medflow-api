using MedFlow.Domain.Model.Enums;

namespace MedFlow.Domain.Exceptions;

public sealed class TransicaoDeStatusInvalidaException(long consultaId, StatusConsulta de, StatusConsulta para)
    : DomainException($"Consulta {consultaId} nao pode transitar de {de} para {para}");
