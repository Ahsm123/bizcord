using System.Collections.Concurrent;
using Bizcord.FileApi.Data;
using Bizcord.FileApi.Models;

namespace Bizcord.FileApi.Tests;

// Same rules as the SQL: only a Pending upload can be marked or completed
public class FakeUploadRepository : IUploadRepository
{
    public readonly ConcurrentDictionary<Guid, Upload> uploads = new();

    public Task AddAsync(Upload upload, CancellationToken ct)
    {
        uploads[upload.Id] = upload;
        return Task.CompletedTask;
    }

    public Task<Upload?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(uploads.GetValueOrDefault(id));

    public Task<bool> MarkMessageRequestedAsync(Guid id, DateTime now, CancellationToken ct) =>
        Task.FromResult(TryUpdatePending(id, u => u with { MessageRequested = true, UpdatedAt = now }));

    public Task<bool> CompleteAsync(Guid id, UploadStatus status, string? rejectionReason, DateTime now,
        CancellationToken ct) =>
        Task.FromResult(TryUpdatePending(id,
            u => u with { Status = status, RejectionReason = rejectionReason, UpdatedAt = now }));

    public Task<IReadOnlyList<Upload>> FindStalledAsync(DateTime updatedBefore, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Upload>>(uploads.Values
            .Where(u => u.Status == UploadStatus.Pending && u.UpdatedAt < updatedBefore)
            .OrderBy(u => u.UpdatedAt).Take(limit).ToList());

    public Task<bool> RejectBeforePivotAsync(Guid id, string reason, DateTime now, CancellationToken ct) =>
        Task.FromResult(TryUpdatePending(id,
            u => u with { Status = UploadStatus.Rejected, RejectionReason = reason, UpdatedAt = now },
            onlyBeforePivot: true));

    private bool TryUpdatePending(Guid id, Func<Upload, Upload> change, bool onlyBeforePivot = false)
    {
        lock (uploads)
        {
            if (!uploads.TryGetValue(id, out var upload) || upload.Status != UploadStatus.Pending
                || (onlyBeforePivot && upload.MessageRequested))
            {
                return false;
            }

            uploads[id] = change(upload);
            return true;
        }
    }
}
