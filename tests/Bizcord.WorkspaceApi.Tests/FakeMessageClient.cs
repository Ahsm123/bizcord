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

    public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default)
    {
        throw new NotImplementedException();
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