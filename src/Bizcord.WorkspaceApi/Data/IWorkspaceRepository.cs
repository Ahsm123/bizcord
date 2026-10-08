using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Data;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct);
    Task SaveAsync(Workspace workspace, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}