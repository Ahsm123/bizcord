using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Models;

public record Member
{
    public required Guid UserId { get; init; }
    public required MemberRole Role { get; init; }
}
