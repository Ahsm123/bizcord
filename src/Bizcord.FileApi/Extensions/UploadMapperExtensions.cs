using Bizcord.Contracts.Files;
using Bizcord.FileApi.Models;

namespace Bizcord.FileApi.Extensions;

public static class UploadMapperExtensions
{
    public static UploadDto ToDto(this Upload upload) =>
        new(upload.Id, upload.WorkspaceId, upload.ChannelId, upload.FileName, upload.SizeBytes,
            upload.Status.ToString(), upload.RejectionReason, upload.CreatedAt);
}
