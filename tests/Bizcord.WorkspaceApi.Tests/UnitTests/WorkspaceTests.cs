using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests;

public class WorkspaceTests
{
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Workspace _workspace;

    public WorkspaceTests()
    {
        _workspace = Workspace.Create(_admin, "test");
        _workspace.AddMember(_admin, _user, MemberRole.User);
    }

    [Fact]
    public void CanDelete_Admin_Succeeds()
    {
        Assert.True(_workspace.CanDelete(_admin).IsSuccess);
    }

    [Fact]
    public void CanDelete_User_IsNotAuthorized()
    {
        Assert.Equal(WorkspaceErrors.NotAuthorized, _workspace.CanDelete(_user).Error);
    }

    [Fact]
    public void CanDelete_NonMember_IsCallerNotAMember()
    {
        Assert.Equal(WorkspaceErrors.CallerNotAMember, _workspace.CanDelete(Guid.NewGuid()).Error);
    }

    [Fact]
    public void Rename_Admin_ChangesName()
    {
        var result = _workspace.Rename(_admin, "renamed");

        Assert.True(result.IsSuccess);
        Assert.Equal("renamed", _workspace.Name);
    }

    [Fact]
    public void Rename_User_IsNotAuthorized_AndKeepsName()
    {
        var result = _workspace.Rename(_user, "renamed");

        Assert.Equal(WorkspaceErrors.NotAuthorized, result.Error);
        Assert.Equal("test", _workspace.Name);
    }

    [Fact]
    public void CreateChannel_Admin_AddsChannel()
    {
        var channelId = Guid.NewGuid();

        var result = _workspace.CreateChannel(_admin, channelId, "general");

        Assert.True(result.IsSuccess);
        Assert.Contains(_workspace.Channels, c => c.Id == channelId && c.Name == "general");
    }

    [Fact]
    public void CreateChannel_User_IsNotAuthorized_AndAddsNothing()
    {
        var result = _workspace.CreateChannel(_user, Guid.NewGuid(), "general");

        Assert.Equal(WorkspaceErrors.NotAuthorized, result.Error);
        Assert.Empty(_workspace.Channels);
    }

    [Fact]
    public void CreateChannel_TakenName_IsDuplicateChannelName()
    {
        _workspace.CreateChannel(_admin, Guid.NewGuid(), "general");

        var result = _workspace.CreateChannel(_admin, Guid.NewGuid(), "general");

        Assert.Equal(WorkspaceErrors.DuplicateChannelName, result.Error);
        Assert.Single(_workspace.Channels);
    }
}
