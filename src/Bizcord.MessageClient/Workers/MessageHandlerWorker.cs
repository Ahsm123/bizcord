using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bizcord.MessageClient.Workers;

// Subscribes to messages of type T and passes each one to the IMessageHandler<T> registrations.
// AddMessaging starts one worker per message type it finds a handler for.
public class MessageHandlerWorker<T>(
    IMessageClient messageClient,
    IServiceScopeFactory scopeFactory,
    string subscriberId) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await messageClient.SubscribeAsync<T>(
            subscriberId,
            async message =>
            {
                // One scope per message, so scoped services (like a repository) are fresh each time
                await using var scope = scopeFactory.CreateAsyncScope();
                foreach (var handler in scope.ServiceProvider.GetServices<IMessageHandler<T>>())
                {
                    await handler.Handle(message, stoppingToken);
                }
            },
            stoppingToken);
    }
}
