using Bizcord.FileApi.Models;

namespace Bizcord.FileApi.Data;

// The status changes are conditional updates on status = 'Pending', so they are atomic. When a late reply,
// a repeated reply and the timeout job race for the same upload, only one of them can move it.
public interface IUploadRepository
{
    Task AddAsync(Upload upload, CancellationToken ct);
    Task<Upload?> GetByIdAsync(Guid id, CancellationToken ct);

    // Records that PostMessage is being sent (the pivot). True while the upload is Pending,
    // also when it was already marked, because sending PostMessage again is safe.
    Task<bool> MarkMessageRequestedAsync(Guid id, DateTime now, CancellationToken ct);

    // Moves a Pending upload to Confirmed or Rejected. False when it was no longer Pending.
    Task<bool> CompleteAsync(Guid id, UploadStatus status, string? rejectionReason, DateTime now,
        CancellationToken ct);
}
