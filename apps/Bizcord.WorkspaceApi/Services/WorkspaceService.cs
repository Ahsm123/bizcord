using Bizcord.WorkspaceApi.Infrastructure;
using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceContracts.Dto;

namespace Bizcord.WorkspaceApi.Services;

public class WorkspaceService(IWorkspaceRepository workspaceRepository) : IWorkspaceService
{
    public async Task<Workspace?> CreateAsync(Guid ownerId, string name)
    {
        // TODO: verify the owner exists in the profile service before creating,
        // and return null when it does not. Blocked until the profile service exists.
        var workspace = Workspace.Create(ownerId, name);
        await workspaceRepository.SaveAsync(workspace);

        return workspace;

    }

    public async Task<Workspace?> GetByIdAsync(Guid id)
    {
        return await workspaceRepository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId)
    {
        return await workspaceRepository.ListForUserAsync(userId);
    }
    
}