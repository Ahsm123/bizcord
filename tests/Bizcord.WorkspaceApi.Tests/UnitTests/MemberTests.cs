using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests;

public class MemberTests
{
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public void Equals_SameUserAndRole_AreEqual()
    {
        var a = new Member { UserId = _user, Role = MemberRole.User };
        var b = new Member { UserId = _user, Role = MemberRole.User };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_SameUserDifferentRole_AreNotEqual()
    {
        var a = new Member { UserId = _user, Role = MemberRole.User };
        var b = new Member { UserId = _user, Role = MemberRole.Admin };

        Assert.NotEqual(a, b);
    }
}
