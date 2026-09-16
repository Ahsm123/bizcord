using Bizcord.WorkspaceApi.Helpers;

namespace Bizcord.WorkspaceApi.Models;

public enum AddMemberResult
{
    Ok,
    CallerNotAMember,
    NotAuthorized,
    AlreadyMember
}

public enum RemoveMemberResult
{
    Ok,
    CallerNotAMember,
    NotAuthorized,
    MemberNotFound,
    LastAdmin
}

public enum CreateChannelResult
{
    Ok,
    CallerNotAMember,
    NotAuthorized,
    DuplicateName
}

public enum RemoveChannelResult
{
    Ok,
    CallerNotAMember,
    NotAuthorized,
    ChannelNotFound
}

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
        workspace._members.Add(new Member { UserId = ownerId, Role = Role.Admin });
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

    public AddMemberResult AddMember(Guid actingUserId, Guid userId, Role newMemberRole)
    {
        var actingUser = _members.FirstOrDefault(m => m.UserId == actingUserId);
        if (actingUser == null)
        {
            return AddMemberResult.CallerNotAMember;
        }

        if (!actingUser.Role.HasPermission(Permission.AddMember))
        {
            return AddMemberResult.NotAuthorized;
        }

        if (newMemberRole != Role.User && !actingUser.Role.HasPermission(Permission.AddAdmin))
        {
            return AddMemberResult.NotAuthorized;
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return AddMemberResult.AlreadyMember;
        }

        var member = new Member() { UserId = userId, Role = newMemberRole };
        _members.Add(member);
        return AddMemberResult.Ok;
    }

    public RemoveMemberResult RemoveMember(Guid actingUserId, Guid memberId)
    {
        var actingUser = _members.FirstOrDefault(m => m.UserId == actingUserId);
        if (actingUser == null)
        {
            return RemoveMemberResult.CallerNotAMember;
        }

        if (!actingUser.Role.HasPermission(Permission.RemoveMember))
        {
            return RemoveMemberResult.NotAuthorized;
        }

        var target = _members.FirstOrDefault(m => m.UserId == memberId);
        if (target is null)
        {
            return RemoveMemberResult.MemberNotFound;
        }

        if (target.Role == Role.Admin && _members.Count(m => m.Role == Role.Admin) == 1)
        {
            return RemoveMemberResult.LastAdmin;
        }

        _members.Remove(target);
        return RemoveMemberResult.Ok;
    }

    public CreateChannelResult CreateChannel(Guid actingUserId, Guid channelId, string name)
    {
        var actingUser = _members.FirstOrDefault(m => m.UserId == actingUserId);
        if (actingUser == null)
        {
            return CreateChannelResult.CallerNotAMember;
        }

        if (!actingUser.Role.HasPermission(Permission.CreateChannel))
        {
            return CreateChannelResult.NotAuthorized;
        }

        if (_channels.Any(c => c.Name == name))
        {
            return CreateChannelResult.DuplicateName;
        }

        var channel = new Channel { Id = channelId, Name = name };
        _channels.Add(channel);
        return CreateChannelResult.Ok;
    }

    public RemoveChannelResult RemoveChannel(Guid actingUserId, Guid channelId)
    {
        var actingUser = _members.FirstOrDefault(m => m.UserId == actingUserId);
        if (actingUser == null)
        {
            return RemoveChannelResult.CallerNotAMember;
        }

        if (!actingUser.Role.HasPermission(Permission.DeleteChannel))
        {
            return RemoveChannelResult.NotAuthorized;
        }

        var channel = _channels.FirstOrDefault(c => c.Id == channelId);
        if (channel is null)
        {
            return RemoveChannelResult.ChannelNotFound;
        }

        _channels.Remove(channel);
        return RemoveChannelResult.Ok;
    }
}