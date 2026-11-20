using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Data;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct);
    // False when the workspace changed in the database since it was loaded (optimistic concurrency, #68).
    // Then nothing is saved, and the caller should load it again.
    Task<bool> SaveAsync(Workspace workspace, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct);
}