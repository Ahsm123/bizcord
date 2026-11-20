namespace Bizcord.Contracts.Events;

// Reason says whether the workspace was full or doesn't exist
public record QuotaExceededEvent(Guid SagaId, Guid WorkspaceId, string Reason);
