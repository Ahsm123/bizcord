using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Bizcord.Shared.Events;

namespace Bizcord.WorkspaceApi.Handlers;

public class MessagePostedHandler(IMessageClient messageClient) : IMessageHandler<MessagePostedEvent>
{
    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        await messageClient.PublishAsync(new ChannelActivityUpdatedEvent()
        {
            MessageId = message.MessageId,
            ChannelId = message.ChannelId,
            ProcessedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}