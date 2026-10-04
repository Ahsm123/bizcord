using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Extensions;

public static class WorkspaceMapperExtension
{
    public static WorkspaceDto ToDto(this Workspace workspace) =>
        new(workspace.Id, workspace.Name);
}