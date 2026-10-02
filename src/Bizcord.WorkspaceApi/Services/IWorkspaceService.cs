using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceContracts.Dto;

namespace Bizcord.WorkspaceApi.Services;

public interface IWorkspaceService
{
    Task<Workspace?> CreateAsync(Guid ownerId, string name);
    Task<Workspace?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId);
    
}