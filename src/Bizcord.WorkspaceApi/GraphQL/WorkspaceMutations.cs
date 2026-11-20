using System.Security.Claims;
using Bizcord.WorkspaceApi.Extensions;
using Bizcord.WorkspaceApi.Services;
using HotChocolate;
using HotChocolate.Authorization;

namespace Bizcord.WorkspaceApi.GraphQL;

[GraphQLName("Mutation")]
public class WorkspaceMutations
{
    // The caller becomes the workspace's admin, like POST api/v1/workspaces
    [Authorize(Policy = Scopes.WorkspaceWrite)]
    public async Task<WorkspaceSummaryType> CreateWorkspace(string name, ClaimsPrincipal user,
        [Service] IWorkspaceService workspaceService, CancellationToken ct)
    {
        var workspace = await workspaceService.CreateAsync(user.GetUserId(), GraphQLErrors.ValidName(name, "workspace"), ct);
        return new WorkspaceSummaryType(workspace.Id, workspace.Name);
    }

    // Only admins can create channels, like POST api/v1/workspaces/{id}/channels
    [Authorize(Policy = Scopes.ChannelWrite)]
    public async Task<ChannelType> CreateChannel(Guid workspaceId, string name, ClaimsPrincipal user,
        [Service] IWorkspaceService workspaceService, CancellationToken ct)
    {
        var result = await workspaceService.CreateChannelAsync(user.GetUserId(), workspaceId,
            GraphQLErrors.ValidName(name, "channel"), ct);
        return result.IsSuccess ? ChannelType.From(result.Value) : throw GraphQLErrors.From(result.Error);
    }
}
