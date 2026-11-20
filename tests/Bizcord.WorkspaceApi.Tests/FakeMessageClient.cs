using Bizcord.MessageClient.Clients;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeMessageClient() : IMessageClient
{
    public readonly List<object> publishedMessages = new();

    // The T each message was published as. RabbitMQ routes on it, so it must be the event's own type.
    public readonly List<Type> publishedTypes = new();

    // Set to make every publish throw, like RabbitMQ being down
    public bool brokerDown;

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        if (brokerDown)
        {
            throw new InvalidOperationException("RabbitMQ is down");
        }

        publishedTypes.Add(typeof(T));
        publishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    // No-op, so hosted workers can start without a broker when the API runs in tests
    public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public T? SinglePublished<T>()
    {
        return publishedMessages.OfType<T>().SingleOrDefault();
    }
}