using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceContracts.Dto;

namespace Bizcord.WorkspaceApi.Infrastructure;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Workspace workspace, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}