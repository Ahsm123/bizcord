using EasyNetQ;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// Captures the first message of type T that matches, on its own auto-delete queue. The match matters, because other
// tests can leave events in WorkspaceApi's outbox that get published during this test (#66). RabbitMQ removes the queue when the capture
// is disposed (or the test process exits), so it doesn't stay behind collecting messages after the test.
// Uses IBus directly because IMessageClient has no way to subscribe with a temporary queue.
public sealed class MessageCapture<T> : IAsyncDisposable
{
    private readonly IAsyncDisposable _subscription;
    private readonly Task<T> _received;

    private MessageCapture(IAsyncDisposable subscription, Task<T> received)
    {
        _subscription = subscription;
        _received = received;
    }

    public static async Task<MessageCapture<T>> StartAsync(IBus bus, Func<T, bool>? match = null)
    {
        var received = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = await bus.PubSub.SubscribeAsync<T>(
            $"capture-{Guid.NewGuid()}",
            (message, _) =>
            {
                if (match is null || match(message))
                {
                    received.TrySetResult(message);
                }

                return Task.CompletedTask;
            },
            x => x.WithAutoDelete(),
            CancellationToken.None);

        return new MessageCapture<T>(subscription, received.Task);
    }

    public Task<T> WaitForMessageAsync(TimeSpan timeout) => _received.WaitAsync(timeout);

    public ValueTask DisposeAsync() => _subscription.DisposeAsync();
}
