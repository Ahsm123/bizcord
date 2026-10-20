using Bizcord.MessageClient.Clients;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeMessageClient() : IMessageClient
{
    public readonly List<object> publishedMessages = new();

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        publishedMessages.Add(message);
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