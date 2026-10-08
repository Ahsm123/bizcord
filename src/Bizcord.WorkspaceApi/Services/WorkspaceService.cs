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

    public async Task<Result> UpdateAsync(Guid actingUserId, Guid workspaceId, string newName, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if(workspace is null)
        {
            return Result.Failure(WorkspaceErrors.NotFound);
        }

        var result = workspace.Rename(actingUserId, newName);
        if(result.IsFailure)
        {
            return result;
        }

        await workspaceRepository.SaveAsync(workspace, ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid actingUserId, Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if(workspace is null)
        {
            return Result.Failure(WorkspaceErrors.NotFound);
        }

        var result = workspace.CanDelete(actingUserId);
        if(result.IsFailure)
        {
            return result;
        }

        await workspaceRepository.DeleteAsync(workspaceId, ct);

        return Result.Success();
    }
}