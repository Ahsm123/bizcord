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
    string subscriberId,
    TimeSpan[]? retryDelays = null) : BackgroundService
{
    private readonly TimeSpan[] _retryDelays =
        retryDelays ?? [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await messageClient.SubscribeAsync<T>(
            subscriberId,
            async message =>
            {
                // One scope per message, so scoped services are fresh each time
                await using var scope = scopeFactory.CreateAsyncScope();
                foreach (var handler in scope.ServiceProvider.GetServices<IMessageHandler<T>>())
                {
                    await HandleWithRetryAsync(handler, message, stoppingToken);
                }
            },
            stoppingToken);
    }

    private async Task HandleWithRetryAsync(IMessageHandler<T> handler, T message, CancellationToken ct)
    {
        foreach (var delay in _retryDelays)
        {
            try
            {
                await handler.Handle(message, ct);
                return;
            }
            catch (Exception)
            {
                await Task.Delay(delay, ct);
            }
        }

        await handler.Handle(message, ct);
    }
}