using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bizcord.MessageClient.Tests.UnitTests;

// #103: a worker that can't subscribe keeps trying instead of failing, which would stop the whole service
public class SubscribeRetryTests
{
    private static readonly TimeSpan[] NoWait = [TimeSpan.FromMilliseconds(1)];

    [Fact]
    public async Task SubscribeFailsThreeTimes_KeepsTryingUntilItIsSubscribed()
    {
        // Arrange
        var client = new UnreachableBrokerClient(failures: 3);
        var worker = CreateWorker(client);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(4, client.Attempts);
        Assert.True(client.Subscribed);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task BrokerNeverComesBack_StoppingTheServiceEndsTheWorkerWithoutAnError()
    {
        // Arrange
        var client = new UnreachableBrokerClient(failures: int.MaxValue);
        var worker = CreateWorker(client);
        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => client.Attempts >= 3);

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert: canceled by the stop, not faulted, so the host doesn't treat it as a crash
        Assert.False(worker.ExecuteTask!.IsFaulted);
        Assert.False(client.Subscribed);
    }

    private static MessageHandlerWorker<TestMessage> CreateWorker(IMessageClient client) =>
        new(client, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MessageHandlerWorker<TestMessage>>.Instance, "test-subscriber", subscribeDelays: NoWait);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    // Throws like EasyNetQ does while RabbitMQ is unreachable, then succeeds
    private sealed class UnreachableBrokerClient(int failures) : IMessageClient
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);
        public bool Subscribed { get; private set; }

        public Task PublishAsync<T>(T message, CancellationToken ct = default) => Task.CompletedTask;

        public Task SubscribeAsync<T>(string subscriberId, Func<T, Task> handler, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _attempts) <= failures)
            {
                throw new TaskCanceledException("None of the specified endpoints were reachable");
            }

            Subscribed = true;
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string subscriberId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
