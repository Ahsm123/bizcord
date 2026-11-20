using System.Reflection;
using System.Text.Json;
using Bizcord.Contracts.Events;
using Bizcord.MessageClient.Clients;
using Microsoft.Extensions.Options;

namespace Bizcord.WorkspaceApi.Outbox;

public class OutboxOptions
{
    // How often the publisher looks for unsent events
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 100;

    // Sent events are kept this long, to see what was sent when, and then deleted
    public TimeSpan KeepSentFor { get; set; } = TimeSpan.FromHours(1);
}

// Sends the events in the outbox to RabbitMQ. Publishing is at-least-once: if WorkspaceApi stops after RabbitMQ
// confirmed an event but before it was marked as sent, the event is sent again, so consumers dedupe on EventId.
public class OutboxDispatcher(
    IOutboxRepository outboxRepository,
    IMessageClient messageClient,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options)
{
    // The event types live in Bizcord.Contracts
    private static readonly Assembly ContractsAssembly = typeof(ChannelActivityUpdatedEvent).Assembly;

    private static readonly MethodInfo PublishMethod =
        typeof(IMessageClient).GetMethod(nameof(IMessageClient.PublishAsync))!;

    public async Task<int> DispatchAsync(CancellationToken ct)
    {
        var sent = await outboxRepository.PublishPendingAsync(m => PublishAsync(m, ct), options.Value.BatchSize, ct);
        await outboxRepository.DeleteSentBeforeAsync(
            timeProvider.GetUtcNow().UtcDateTime - options.Value.KeepSentFor, ct);
        return sent;
    }

    // RabbitMQ routes on the message type, so the event is published with its own type and not as object
    private Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        var type = ContractsAssembly.GetType(message.Type)
                   ?? throw new InvalidOperationException($"Unknown outbox event type {message.Type}");
        var payload = JsonSerializer.Deserialize(message.Payload, type)!;
        return (Task)PublishMethod.MakeGenericMethod(type).Invoke(messageClient, [payload, ct])!;
    }
}

// Runs the dispatcher every PollInterval. While RabbitMQ is down the events wait in the outbox and go out
// when it is back.
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Publishing from the outbox failed, the events stay in the outbox and are tried again");
            }
        }
    }
}
