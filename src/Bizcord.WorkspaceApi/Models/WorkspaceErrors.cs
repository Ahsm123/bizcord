namespace Bizcord.WorkspaceApi.Models;

public static class WorkspaceErrors
{
    public static readonly Error CallerNotAMember = new("Workspace.CallerNotAMember", "Caller is not a member of the workspace");
    public static readonly Error AlreadyMember = new("Workspace.AlreadyMember", "User is already a member");
    public static readonly Error MemberNotFound = new("Workspace.MemberNotFound", "Member was not found");
    public static readonly Error LastAdmin = new("Workspace.LastAdmin", "Cannot remove the last admin");
    public static readonly Error DuplicateChannelName = new("Workspace.DuplicateChannelName", "A channel with that name already exists");
    public static readonly Error ChannelNotFound = new("Workspace.ChannelNotFound", "Channel was not found");
    public static readonly Error NotAuthorized = new("Workspace.NotAuthorized", "Missing permission for this action");
    public static readonly Error NotFound = new("Workspace.NotFound", "Workspace was not found");
}
