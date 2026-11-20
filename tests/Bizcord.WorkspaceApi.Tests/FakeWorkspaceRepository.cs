using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceApi.Outbox;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeWorkspaceRepository : IWorkspaceRepository
{
    public readonly Dictionary<Guid, Workspace> workspaces = new();
    public readonly List<(Guid ChannelId, DateTime PostedAt)> activityUpdates = new();
    public bool updateResult = true;
    public bool hang;

    // The events the repository would have written to the outbox in the same transaction as the update
    public readonly List<OutboxMessage> outbox = new();

    public Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, OutboxMessage activityUpdated,
        CancellationToken ct)
    {
        activityUpdates.Add((channelId, postedAt));
        if (updateResult)
        {
            outbox.Add(activityUpdated);
        }

        return Task.FromResult(updateResult);
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // Simulates a Postgres that never responds, until the request timeout cancels ct
        if (hang)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        return workspaces.GetValueOrDefault(id);
    }

    // Set to make the next save fail as if another request changed the workspace first (#68).
    // The real version check is tested against Postgres, since the fake hands out the same instance to every load.
    public bool conflictOnNextSave;

    public readonly Dictionary<(Guid UserId, string Key), Guid> idempotencyKeys = new();

    public async Task<Guid> CreateAsync(Workspace workspace, Guid userId, string idempotencyKey, CancellationToken ct)
    {
        if (idempotencyKeys.TryGetValue((userId, idempotencyKey), out var existing))
        {
            return existing;
        }

        await SaveAsync(workspace, ct);
        idempotencyKeys[(userId, idempotencyKey)] = workspace.Id;
        return workspace.Id;
    }

    public Task<bool> SaveAsync(Workspace workspace, CancellationToken ct)
    {
        if (conflictOnNextSave)
        {
            conflictOnNextSave = false;
            return Task.FromResult(false);
        }

        workspaces[workspace.Id] = workspace;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        IReadOnlyList<WorkspaceDto> result = workspaces.Values
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .Select(w => new WorkspaceDto(w.Id, w.Name))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        return Task.FromResult(workspaces.Remove(id));
    }

    public Task<bool> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct) =>
        Task.FromResult(workspaces.Values.Any(w =>
            w.Channels.Any(c => c.Id == channelId) && w.Members.Any(m => m.UserId == userId)));
}
