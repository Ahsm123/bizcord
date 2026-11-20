using System.ComponentModel.DataAnnotations;

namespace Bizcord.FileApi.Requests;

// The client says how big the file is. In a real upload you only know that once the file has arrived,
// which is a deliberate simplification (see docs/decisions.md).
public record CreateUploadRequest(
    Guid WorkspaceId,
    Guid ChannelId,
    [Required(AllowEmptyStrings = false), MaxLength(255)] string FileName,
    [Range(1, long.MaxValue)] long SizeBytes,
    [MaxLength(2000)] string? Message) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (WorkspaceId == Guid.Empty)
        {
            yield return new ValidationResult("WorkspaceId must be a non-empty Guid", [nameof(WorkspaceId)]);
        }

        if (ChannelId == Guid.Empty)
        {
            yield return new ValidationResult("ChannelId must be a non-empty Guid", [nameof(ChannelId)]);
        }
    }
}
