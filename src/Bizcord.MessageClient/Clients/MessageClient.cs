using System.Collections.Concurrent;
using EasyNetQ;

namespace Bizcord.MessageClient.Clients;

public class MessageClient(IBus bus) : IMessageClient
{
    // Concurrent, since hosted workers subscribe from background threads at the same time
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _subscriptions = new();

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        var task = bus.PubSub.PublishAsync(message, ct);
        return task;
    }

    public async Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default)
    {
        if (_subscriptions.ContainsKey(subscriberId))
        {
            throw new ArgumentException($"The subscriber {subscriberId} is already subscribed");
        }
        var handle = await bus.PubSub.SubscribeAsync(subscriberId, handler, ct);
        if (!_subscriptions.TryAdd(subscriberId, handle))
        {
            // Another call subscribed the same id while this one was waiting on the broker
            await handle.DisposeAsync();
            throw new ArgumentException($"The subscriber {subscriberId} is already subscribed");
        }
    }

    public async Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default)
    {
        if (_subscriptions.TryRemove(subscriberId, out var handle))
            await handle.DisposeAsync();
    }
}
