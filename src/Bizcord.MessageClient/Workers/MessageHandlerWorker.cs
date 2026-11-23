using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bizcord.MessageClient.Workers;

// Subscribes to messages of type T and passes each one to the IMessageHandler<T> registrations.
// AddMessaging starts one worker per message type it finds a handler for.
public class MessageHandlerWorker<T>(
    IMessageClient messageClient,
    IServiceScopeFactory scopeFactory,
    ILogger<MessageHandlerWorker<T>> logger,
    string subscriberId,
    TimeSpan[]? retryDelays = null,
    TimeSpan[]? subscribeDelays = null) : BackgroundService
{
    private readonly TimeSpan[] _retryDelays =
        retryDelays ?? [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    // The waits between attempts to subscribe. The last one repeats until it works.
    private readonly TimeSpan[] _subscribeDelays = subscribeDelays ??
    [
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    // Keeps trying to subscribe while RabbitMQ is unreachable (#103). Letting the exception out would make .NET stop
    // the whole service, HTTP included, and it wouldn't come back when RabbitMQ does.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1;; attempt++)
        {
            try
            {
                await messageClient.SubscribeAsync<T>(subscriberId, message => HandleAsync(message, stoppingToken),
                    stoppingToken);
                if (attempt > 1)
                {
                    logger.LogInformation("Subscribed {SubscriberId} on attempt {Attempt}", subscriberId, attempt);
                }

                return;
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                var delay = _subscribeDelays[Math.Min(attempt - 1, _subscribeDelays.Length - 1)];
                logger.LogWarning(e, "Could not subscribe {SubscriberId} on attempt {Attempt}, trying again in {Delay}",
                    subscriberId, attempt, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task HandleAsync(T message, CancellationToken stoppingToken)
    {
        // One scope per message, so scoped services are fresh each time
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IMessageHandler<T>>())
        {
            await HandleWithRetryAsync(handler, message, stoppingToken);
        }
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
