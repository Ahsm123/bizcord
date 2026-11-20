using Bizcord.MessageClient.Clients;

namespace Bizcord.FileApi.Tests;

public class FakeMessageClient : IMessageClient
{
    public readonly List<object> publishedMessages = new();

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        lock (publishedMessages)
        {
            publishedMessages.Add(message!);
        }

        return Task.CompletedTask;
    }

    // No-op, so hosted workers can start without a broker when the API runs in tests
    public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default) => Task.CompletedTask;

    public List<T> Published<T>()
    {
        lock (publishedMessages)
        {
            return publishedMessages.OfType<T>().ToList();
        }
    }
}
