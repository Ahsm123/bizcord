namespace Bizcord.Contracts.Events;

public record QuotaReservedEvent(Guid SagaId, Guid WorkspaceId, long SizeBytes);
