using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Data;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Workspace workspace, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct = default);
}