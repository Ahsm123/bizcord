using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeWorkspaceRepository : IWorkspaceRepository
{
    public readonly List<(Guid ChannelId, DateTime PostedAt)> activityUpdates = new();

    public Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct)
    {
        activityUpdates.Add((channelId, postedAt));
        return Task.FromResult(true);
    }

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(Workspace workspace, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}