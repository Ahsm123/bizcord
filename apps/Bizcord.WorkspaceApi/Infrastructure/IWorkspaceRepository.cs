using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Infrastructure;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Workspace workspace, CancellationToken ct = default);
}