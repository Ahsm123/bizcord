namespace Bizcord.Contracts.Files;

// Status is Pending, Confirmed or Rejected. RejectionReason is only set for Rejected.
public record UploadDto(
    Guid Id,
    Guid WorkspaceId,
    Guid ChannelId,
    string FileName,
    long SizeBytes,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt);
