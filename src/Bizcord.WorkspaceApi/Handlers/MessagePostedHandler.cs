using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Bizcord.Shared.Events;
using Bizcord.WorkspaceApi.Data;

namespace Bizcord.WorkspaceApi.Handlers;

public class MessagePostedHandler(
    IMessageClient messageClient,
    IWorkspaceRepository repository) : IMessageHandler<MessagePostedEvent>
{
    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        var updated = await repository.UpdateChannelLastActivityAsync(message.ChannelId, message.PostedAt, cancellationToken);
        if (!updated)
            return;

        await messageClient.PublishAsync(
            new ChannelActivityUpdatedEvent(
                message.MessageId,
                message.ChannelId,
                message.PostedAt),
            cancellationToken);
    }
}