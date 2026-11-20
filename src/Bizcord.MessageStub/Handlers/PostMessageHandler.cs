using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Microsoft.Extensions.Options;

namespace Bizcord.MessageStub.Handlers;

// Step 3 of the upload saga. Replies to File with PostMessageSucceededEvent and tells everyone else with
// MessagePostedEvent, as a real Message service would.
public class PostMessageHandler(
    PostedMessages postedMessages,
    IMessageClient messageClient,
    IOptionsMonitor<MessageStubOptions> options,
    TimeProvider timeProvider,
    ILogger<PostMessageHandler> logger) : IMessageHandler<PostMessage>
{
    public const string FailReason = "The message stub is set up to fail (MessageStub:Behavior)";

    public async Task Handle(PostMessage message, CancellationToken cancellationToken)
    {
        switch (options.CurrentValue.Behavior)
        {
            case StubBehavior.NoReply:
                logger.LogWarning("Ignoring PostMessage for saga {SagaId} (MessageStub:Behavior is NoReply)",
                    message.SagaId);
                return;
            case StubBehavior.Fail:
                await messageClient.PublishAsync(new PostMessageFailedEvent(message.SagaId, FailReason),
                    cancellationToken);
                return;
        }

        var messageId = postedMessages.MessageIdFor(message.SagaId);

        if (postedMessages.TryClaimAnnouncement(message.SagaId))
        {
            try
            {
                await messageClient.PublishAsync(new MessagePostedEvent(messageId, message.ChannelId,
                    message.AuthorId, message.Content, timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
            }
            catch
            {
                postedMessages.Forget(message.SagaId);
                throw;
            }
        }
        else
        {
            logger.LogInformation("PostMessage for saga {SagaId} was already posted as {MessageId}",
                message.SagaId, messageId);
        }

        // Also sent for a repeated command, in case File never got the first reply
        await messageClient.PublishAsync(new PostMessageSucceededEvent(message.SagaId, messageId), cancellationToken);
    }
}
