namespace Bizcord.Contracts.Commands;

// File asks Workspace to reserve storage for an upload (step 2 of the upload saga)
public record ReserveQuota(Guid SagaId, Guid WorkspaceId, long SizeBytes);
