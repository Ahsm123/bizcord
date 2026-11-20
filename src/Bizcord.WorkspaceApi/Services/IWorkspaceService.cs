using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Services;

public interface IWorkspaceService
{
    // With an idempotency key, a repeated call returns the workspace from the first call instead of a new one (#67)
    Task<Workspace> CreateAsync(Guid ownerId, string name, CancellationToken ct, string? idempotencyKey = null);
    Task<Result<Workspace>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<Result> UpdateAsync(Guid actingUserId, Guid workspaceId, string newName, CancellationToken ct);
    Task<Result> DeleteAsync(Guid actingUserId, Guid workspaceId, CancellationToken ct);
    Task<Result<Channel>> CreateChannelAsync(Guid actingUserId, Guid workspaceId, string name, CancellationToken ct);
    Task RecordChannelActivityAsync(MessagePostedEvent message, CancellationToken ct);
    Task<Result> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct);
}