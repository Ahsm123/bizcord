namespace Bizcord.WorkspaceApi.Models;

public class Member
{
    public required Guid UserId { get; init; }
    public required Role Role { get; init; }
}