using EasyNetQ;

namespace Bizcord.MessageClient.Tests;

public class FakeBus : IBus
{
    public IPubSub PubSub { get; } = new FakePubSub();
    public IRpc Rpc => throw new NotImplementedException();
    public ISendReceive SendReceive => throw new NotImplementedException();
    public IScheduler Scheduler => throw new NotImplementedException();
    public IAdvancedBus Advanced => throw new NotImplementedException();
}

public class FakePubSub : IPubSub
{
    public Task PublishAsync<T>(T message, Action<IPublishConfiguration> configure, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<SubscriptionResult> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, CancellationToken, Task> onMessage,
        Action<ISubscriptionConfiguration> configure,
        CancellationToken ct)
    {
        return Task.FromResult(new SubscriptionResult(default, default, new FakeSubscription()));
    }
}

public class FakeSubscription : IAsyncDisposable
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
