namespace Bizcord.Contracts.Commands;

// Compensation for ReserveQuota. Workspace knows the workspace and size from the reservation,
// so the saga id is enough. Releasing a reservation that doesn't exist does nothing.
public record ReleaseQuota(Guid SagaId);
