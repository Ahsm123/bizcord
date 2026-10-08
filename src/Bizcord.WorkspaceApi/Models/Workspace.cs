using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Extensions;

namespace Bizcord.WorkspaceApi.Models;

public class Workspace
{
    private Workspace(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    private readonly List<Channel> _channels = [];
    public IReadOnlyList<Channel> Channels => _channels;
    private readonly List<Member> _members = [];
    public IReadOnlyList<Member> Members => _members;

    // AddMember checks the caller against the existing members, so the first
    // member cant go through it. So instead the owner is added directly here instead.
    // Since there is nobody to authorize the first member against.
    public static Workspace Create(Guid ownerId, string name)
    {
        var workspace = new Workspace(Guid.NewGuid(), name);
        workspace._members.Add(new Member { UserId = ownerId, Role = MemberRole.Admin });
        return workspace;
    }

    // Rebuilds a workspace from stored state. No rules and no authorization run
    // here, this data was already validated when it was written.
    internal static Workspace Rehydrate(Guid id, string name, IEnumerable<Member> members, IEnumerable<Channel> channels)
    {
        var workspace = new Workspace(id, name);
        workspace._members.AddRange(members);
        workspace._channels.AddRange(channels);
        return workspace;
    }

    public Result CanDelete(Guid actingUserId) => Authorize(actingUserId, Permission.DeleteWorkspace);

    public Result AddMember(Guid actingUserId, Guid userId, MemberRole newMemberRole)
    {
        var permission = newMemberRole == MemberRole.User ? Permission.AddMember : Permission.AddAdmin;
        var authorized = Authorize(actingUserId, permission);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return Result.Failure(WorkspaceErrors.AlreadyMember);
        }

        var member = new Member() { UserId = userId, Role = newMemberRole };
        _members.Add(member);

        return Result.Success();
    }

    public Result RemoveMember(Guid actingUserId, Guid memberId)
    {
        var authorized = Authorize(actingUserId, Permission.RemoveMember);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        var target = _members.FirstOrDefault(m => m.UserId == memberId);
        if (target is null)
        {
            return Result.Failure(WorkspaceErrors.MemberNotFound);
        }

        if (target.Role == MemberRole.Admin && _members.Count(m => m.Role == MemberRole.Admin) == 1)
        {
            return Result.Failure(WorkspaceErrors.LastAdmin);
        }

        _members.Remove(target);

        return Result.Success();
    }

    public Result CreateChannel(Guid actingUserId, Guid channelId, string name)
    {
        var authorized = Authorize(actingUserId, Permission.CreateChannel);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        if (_channels.Any(c => c.Name == name))
        {
            return Result.Failure(WorkspaceErrors.DuplicateChannelName);
        }

        var channel = new Channel { Id = channelId, Name = name };
        _channels.Add(channel);

        return Result.Success();
    }

    public Result RemoveChannel(Guid actingUserId, Guid channelId)
    {
        var authorized = Authorize(actingUserId, Permission.DeleteChannel);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        var channel = _channels.FirstOrDefault(c => c.Id == channelId);
        if (channel is null)
        {
            return Result.Failure(WorkspaceErrors.ChannelNotFound);
        }

        _channels.Remove(channel);

        return Result.Success();
    }

    public Result Rename(Guid actingUserId, string newName)
    {
        var authorized = Authorize(actingUserId, Permission.UpdateWorkspace);
        if (authorized.IsFailure)
        {
            return authorized;
        }

        Name = newName;
        return Result.Success();

    }

    private Result Authorize(Guid actingUserId, Permission permission)
    {
        var actingUser = _members.FirstOrDefault(m => m.UserId == actingUserId);
        if (actingUser == null)
        {
            return Result.Failure(WorkspaceErrors.CallerNotAMember);
        }

        if (!actingUser.Role.HasPermission(permission))
        {
            return Result.Failure(WorkspaceErrors.NotAuthorized);
        }

        return Result.Success();
    }
}
