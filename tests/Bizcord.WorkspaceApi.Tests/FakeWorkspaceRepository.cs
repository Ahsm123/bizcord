using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeWorkspaceRepository : IWorkspaceRepository
{
    public readonly Dictionary<Guid, Workspace> workspaces = new();
    public readonly List<(Guid ChannelId, DateTime PostedAt)> activityUpdates = new();
    public bool updateResult = true;

    public Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt, CancellationToken ct)
    {
        activityUpdates.Add((channelId, postedAt));
        return Task.FromResult(updateResult);
    }

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return Task.FromResult(workspaces.GetValueOrDefault(id));
    }

    public Task SaveAsync(Workspace workspace, CancellationToken ct)
    {
        workspaces[workspace.Id] = workspace;
        return Task.CompletedTask;
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
}
