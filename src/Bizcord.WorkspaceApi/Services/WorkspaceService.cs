using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Services;

public class WorkspaceService(IWorkspaceRepository workspaceRepository) : IWorkspaceService
{
    public async Task<Workspace> CreateAsync(Guid ownerId, string name, CancellationToken ct)
    {
        // TODO: verify the owner exists in the profile service before creating,
        // TODO: and return null when it does not. Blocked until the profile service exists.
        var workspace = Workspace.Create(ownerId, name);
        await workspaceRepository.SaveAsync(workspace, ct);

        return workspace;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await workspaceRepository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        return await workspaceRepository.ListForUserAsync(userId, ct);
    }
}