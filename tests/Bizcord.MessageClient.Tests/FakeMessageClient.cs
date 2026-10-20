using Bizcord.MessageClient.Clients;

namespace Bizcord.MessageClient.Tests;

// Keeps the subscribed handlers, so tests can deliver messages to them without a broker
public class FakeMessageClient : IMessageClient
{
    public readonly Dictionary<string, Delegate> subscriptions = new();

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default)
    {
        subscriptions.Add(subscriberId, handler);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default)
    {
        subscriptions.Remove(subscriberId);
        return Task.CompletedTask;
    }

    public Task DeliverAsync<T>(string subscriberId, T message)
    {
        return ((Func<T, Task>)subscriptions[subscriberId])(message);
    }
}
