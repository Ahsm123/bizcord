using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Bizcord.Shared.Events;

namespace Bizcord.WorkspaceApi.Workers;

public class MessagePostedWorker(
    IMessageClient  messageClient,
    IMessageHandler<MessagePostedEvent> messageHandler) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await messageClient.SubscribeAsync<MessagePostedEvent>(
            "WorkspaceMessagePosted", 
            message => messageHandler.Handle(message, stoppingToken), 
            stoppingToken);
    }
}