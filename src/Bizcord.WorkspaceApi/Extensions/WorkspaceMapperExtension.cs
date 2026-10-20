using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Extensions;

public static class WorkspaceMapperExtension
{
    public static WorkspaceDto ToDto(this Workspace workspace) =>
        new(workspace.Id, workspace.Name);

    // Channel doesn't store its workspace id since it only lives inside a workspace, so the caller passes it in
    public static ChannelDto ToDto(this Channel channel, Guid workspaceId) =>
        new(channel.Id, channel.Name, workspaceId);
}