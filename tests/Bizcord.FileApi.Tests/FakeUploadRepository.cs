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

    private bool TryUpdatePending(Guid id, Func<Upload, Upload> change)
    {
        lock (uploads)
        {
            if (!uploads.TryGetValue(id, out var upload) || upload.Status != UploadStatus.Pending)
            {
                return false;
            }

            uploads[id] = change(upload);
            return true;
        }
    }
}
