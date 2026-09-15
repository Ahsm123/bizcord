using Bizcord.WorkspaceApi.Helpers;

namespace Bizcord.WorkspaceApi.Models;

public class Workspace
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    
    private readonly List<Channel> _channels = [];
    public IReadOnlyList<Channel> Channels => _channels;

    private readonly List<Member> _members = [];
    public IReadOnlyList<Member> Members => _members;

    public Member? AddMember(Role actingRole, Guid userId, Role newMemberRole)
    {
        if (!actingRole.HasPermission(Permission.AddMember))
        {
            return null;
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return null;
        }

        var member = new Member() { UserId = userId, Role = newMemberRole };
        _members.Add(member);
        return member;
    }
    
    public bool RemoveMember(Role actingRole, Guid memberId)
    {
        if (!actingRole.HasPermission(Permission.RemoveMember))
        {
            return false;
        }
        return _members.RemoveAll(m => m.UserId == memberId) > 0;
    }

    public Channel? CreateChannel(Role actingRole, string name)
    {
        if (!actingRole.HasPermission(Permission.CreateChannel))
        {
            return null;
        }
        
        var channel = new Channel { Id = Guid.NewGuid(), Name = name };
        _channels.Add(channel);
        return channel;
    }

    public bool RemoveChannel(Role actingRole, Guid channelId)
    {
        if (!actingRole.HasPermission(Permission.DeleteChannel))
        {
            return false;
        }
        return _channels.RemoveAll(c => c.Id == channelId) > 0;
    }
}
