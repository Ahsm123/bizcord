namespace Bizcord.FileApi.Models;

public enum UploadStatus
{
    Pending,
    Confirmed,
    Rejected
}

// FileApi only stores the metadata of a file, not the file itself (see docs/decisions.md).
// Status changes happen as conditional updates in IUploadRepository, so a late reply and the timeout job
// can never both move the same upload.
public sealed record Upload(
    Guid Id,
    Guid UploaderId,
    Guid WorkspaceId,
    Guid ChannelId,
    string FileName,
    long SizeBytes,
    string Message,
    UploadStatus Status,
    bool MessageRequested,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static Upload Start(Guid uploaderId, Guid workspaceId, Guid channelId, string fileName, long sizeBytes,
        string? message, DateTime now) =>
        new(Guid.NewGuid(), uploaderId, workspaceId, channelId, fileName.Trim(), sizeBytes, message?.Trim() ?? "",
            UploadStatus.Pending, MessageRequested: false, RejectionReason: null, now, now);

    // The text of the message that is posted in the channel, with a reference to the file
    public string MessageContent() =>
        string.IsNullOrEmpty(Message) ? $"[file] {FileName}" : $"{Message}\n[file] {FileName}";
}
