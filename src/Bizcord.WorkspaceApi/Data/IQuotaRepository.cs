namespace Bizcord.WorkspaceApi.Data;

public enum ReservationOutcome
{
    Reserved,
    Exceeded,
    WorkspaceNotFound,
    AlreadyReleased
}

// The workspace storage quota for the upload saga. Separate from IWorkspaceRepository, because the quota is changed
// with single atomic updates and never by loading and saving the Workspace aggregate.
public interface IQuotaRepository
{
    // Reserves the space once per saga. A repeated call returns the outcome of the first one.
    Task<ReservationOutcome> ReserveAsync(Guid sagaId, Guid workspaceId, long sizeBytes, CancellationToken ct);

    // Gives the space back once per saga. Returns false when there was no reserved space to release.
    Task<bool> ReleaseAsync(Guid sagaId, CancellationToken ct);
}
