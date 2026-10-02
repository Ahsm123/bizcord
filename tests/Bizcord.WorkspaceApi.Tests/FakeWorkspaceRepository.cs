using Bizcord.WorkspaceApi.Infrastructure;
using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceContracts.Dto;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeWorkspaceRepository : IWorkspaceRepository
{
    public readonly List<(Guid ChannelId, DateTime PostedAt)> activityUpdates = new();

    public Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct = default)
    {
        activityUpdates.Add((channelId, postedAt));
        return Task.FromResult(true);
    }

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveAsync(Workspace workspace, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}