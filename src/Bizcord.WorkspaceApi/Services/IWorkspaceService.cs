using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Services;

public interface IWorkspaceService
{
    Task<Workspace> CreateAsync(Guid ownerId, string name, CancellationToken ct);
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(Guid userId, CancellationToken ct);
    
}