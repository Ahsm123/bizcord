using Bizcord.Contracts.Commands;
using Bizcord.FileApi.Data;
using Bizcord.FileApi.Models;
using Bizcord.FileApi.Requests;
using Bizcord.MessageClient.Clients;

namespace Bizcord.FileApi.Services;

public interface IUploadService
{
    Task<Upload> StartAsync(Guid uploaderId, CreateUploadRequest request, CancellationToken ct);

    // Null when the upload doesn't exist or belongs to someone else, so others can't tell it exists
    Task<Upload?> GetForUploaderAsync(Guid uploaderId, Guid uploadId, CancellationToken ct);
}

public class UploadService(IUploadRepository uploadRepository, IMessageClient messageClient, TimeProvider timeProvider)
    : IUploadService
{
    // Step 1 of the upload saga (docs/upload-saga.md): store the upload as Pending and ask Workspace for space
    public async Task<Upload> StartAsync(Guid uploaderId, CreateUploadRequest request, CancellationToken ct)
    {
        var upload = Upload.Start(uploaderId, request.WorkspaceId, request.ChannelId, request.FileName,
            request.SizeBytes, request.Message, timeProvider.GetUtcNow().UtcDateTime);

        await uploadRepository.AddAsync(upload, ct);

        // If this publish fails, the upload is stored but no command was sent. It stays Pending until the
        // timeout job rejects it, so the saga still ends.
        await messageClient.PublishAsync(new ReserveQuota(upload.Id, upload.WorkspaceId, upload.SizeBytes), ct);

        return upload;
    }

    public async Task<Upload?> GetForUploaderAsync(Guid uploaderId, Guid uploadId, CancellationToken ct)
    {
        var upload = await uploadRepository.GetByIdAsync(uploadId, ct);
        return upload?.UploaderId == uploaderId ? upload : null;
    }
}
