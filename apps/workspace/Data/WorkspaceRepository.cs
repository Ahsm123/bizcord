using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Data;

public class WorkspaceRepository : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(Workspace workspace, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}