using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Data;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Workspace workspace, CancellationToken ct = default);
}