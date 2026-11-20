using System.Security.Claims;
using Bizcord.WorkspaceApi.Extensions;
using Bizcord.WorkspaceApi.Services;
using HotChocolate;
using HotChocolate.Authorization;

namespace Bizcord.WorkspaceApi.GraphQL;

// A presentation layer next to the REST controllers (BFF style). The resolvers only call IWorkspaceService,
// so there is no business logic here and no repository calls.
[GraphQLName("Query")]
public class WorkspaceQueries
{
    // The workspaces the caller is a member of
    [Authorize(Policy = Scopes.WorkspaceRead)]
    public async Task<IReadOnlyList<WorkspaceSummaryType>> GetMyWorkspaces(ClaimsPrincipal user,
        [Service] IWorkspaceService workspaceService, CancellationToken ct)
    {
        var workspaces = await workspaceService.GetAllAsync(user.GetUserId(), ct);
        return workspaces.Select(w => new WorkspaceSummaryType(w.Id, w.Name)).ToList();
    }

    // One workspace with its channels and members, which takes two REST calls without GraphQL
    [Authorize(Policy = Scopes.WorkspaceRead)]
    public async Task<WorkspaceType> GetWorkspace(Guid id, ClaimsPrincipal user,
        [Service] IWorkspaceService workspaceService, CancellationToken ct)
    {
        var result = await workspaceService.GetByIdAsync(id, user.GetUserId(), ct);
        return result.IsSuccess ? WorkspaceType.From(result.Value) : throw GraphQLErrors.From(result.Error);
    }
}
