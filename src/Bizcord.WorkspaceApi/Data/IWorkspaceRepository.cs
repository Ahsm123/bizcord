using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Data;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct);
    // False when the workspace changed in the database since it was loaded (optimistic concurrency, #68).
    // Then nothing is saved, and the caller should load it again.
    Task<bool> SaveAsync(Workspace workspace, CancellationToken ct);

    // Saves a new workspace together with the user's idempotency key (#67). If the user already used the key,
    // nothing is saved and the id of the workspace the key created the first time is returned instead.
    Task<Guid> CreateAsync(Workspace workspace, Guid userId, string idempotencyKey, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct);
}