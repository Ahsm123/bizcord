using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Bizcord.Contracts.Events;

namespace Bizcord.WorkspaceApi.Workers;

public class MessagePostedWorker(
    IMessageClient  messageClient,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await messageClient.SubscribeAsync<MessagePostedEvent>(
            "WorkspaceMessagePosted",
            async message =>
            {
                // One scope per message, so scoped services (the repository) are fresh each time
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<MessagePostedEvent>>();
                await handler.Handle(message, stoppingToken);
            },
            stoppingToken);
    }
}