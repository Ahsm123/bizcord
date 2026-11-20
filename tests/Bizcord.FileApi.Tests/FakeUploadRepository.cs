using System.Collections.Concurrent;
using Bizcord.FileApi.Data;
using Bizcord.FileApi.Models;

namespace Bizcord.FileApi.Tests;

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
}
