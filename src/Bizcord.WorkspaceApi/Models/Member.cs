using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Models;

public class Member
{
    public required Guid UserId { get; init; }
    public required MemberRole Role { get; init; }
}
