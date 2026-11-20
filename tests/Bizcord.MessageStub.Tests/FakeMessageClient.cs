using Bizcord.MessageClient.Clients;

namespace Bizcord.MessageStub.Tests;

public class FakeMessageClient : IMessageClient
{
    public readonly List<object> publishedMessages = new();

    // Set to make the next publish of that type throw once, like a broker that is briefly down
    public Type? FailNextPublishOf { get; set; }

    public Task PublishAsync<T>(T message, CancellationToken ct = default)
    {
        if (FailNextPublishOf == typeof(T))
        {
            FailNextPublishOf = null;
            throw new InvalidOperationException("Broker unavailable");
        }

        publishedMessages.Add(message!);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default) => Task.CompletedTask;

    public List<T> Published<T>() => publishedMessages.OfType<T>().ToList();
}
