using Bizcord.MessageClient.Clients;
using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Services;

public class WorkspaceService(IWorkspaceRepository workspaceRepository, IMessageClient messageClient)
    : IWorkspaceService
{
    public async Task<Workspace> CreateAsync(Guid ownerId, string name, CancellationToken ct)
    {
        // TODO: verify the owner exists in the profile service before creating,
        // TODO: and return null when it does not. Blocked until the profile service exists.
        var workspace = Workspace.Create(ownerId, name);
        if (!await workspaceRepository.SaveAsync(workspace, ct))
        {
            // A new workspace gets a new id, so nothing else can have saved it first
            throw new InvalidOperationException($"Workspace {workspace.Id} already exists");
        }

        return workspace;
    }

    public async Task<Result<Workspace>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(id, ct);
        if (workspace is null)
        {
            return Result<Workspace>.Failure(WorkspaceErrors.NotFound);
        }

        var result = workspace.CanRead(userId);
        if (result.IsFailure)
        {
            return Result<Workspace>.Failure(result.Error);
        }

        return Result<Workspace>.Success(workspace);
    }

    public async Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        return await workspaceRepository.ListForUserAsync(userId, ct);
    }

    public async Task<Result> UpdateAsync(Guid actingUserId, Guid workspaceId, string newName, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
        {
            return Result.Failure(WorkspaceErrors.NotFound);
        }

        var result = workspace.Rename(actingUserId, newName);
        if (result.IsFailure)
        {
            return result;
        }

        // False when another request changed the workspace after it was loaded here (#68)
        return await workspaceRepository.SaveAsync(workspace, ct)
            ? Result.Success()
            : Result.Failure(WorkspaceErrors.ConcurrentChange);
    }

    public async Task<Result> DeleteAsync(Guid actingUserId, Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
        {
            return Result.Failure(WorkspaceErrors.NotFound);
        }

        var result = workspace.CanDelete(actingUserId);
        if (result.IsFailure)
        {
            return result;
        }

        await workspaceRepository.DeleteAsync(workspaceId, ct);

        return Result.Success();
    }

    public async Task<Result<Channel>> CreateChannelAsync(Guid actingUserId, Guid workspaceId, string name,
        CancellationToken ct)
    {
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
        {
            return Result<Channel>.Failure(WorkspaceErrors.NotFound);
        }

        var channelId = Guid.NewGuid();
        var result = workspace.CreateChannel(actingUserId, channelId, name);
        if (result.IsFailure)
        {
            return Result<Channel>.Failure(result.Error);
        }

        // Saving also creates the channel's channel_activity row, so MessagePostedEvents for it get recorded.
        // False when another request changed the workspace after it was loaded here (#68).
        if (!await workspaceRepository.SaveAsync(workspace, ct))
        {
            return Result<Channel>.Failure(WorkspaceErrors.ConcurrentChange);
        }

        return Result<Channel>.Success(workspace.Channels.Single(c => c.Id == channelId));
    }

    public async Task RecordChannelActivityAsync(MessagePostedEvent message, CancellationToken ct)
    {
        var updated = await workspaceRepository.UpdateChannelLastActivityAsync(message.ChannelId, message.PostedAt, ct);
        if (!updated)
        {
            return;
        }

        await messageClient.PublishAsync(
            new ChannelActivityUpdatedEvent(message.MessageId, message.ChannelId, message.PostedAt), ct);
    }

    public async Task<Result> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct)
    {
        var result = await workspaceRepository.IsChannelMemberAsync(userId, channelId, ct);
        if (!result)
        {
            return Result.Failure(WorkspaceErrors.CallerNotAMember);
        }

        return Result.Success();
    }
}