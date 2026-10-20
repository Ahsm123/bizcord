using Bizcord.MessageClient.Clients;

namespace Bizcord.MessageClient.Tests.UnitTests;

public class MessageClientTests
{
    private readonly IMessageClient _messageClient = new Clients.MessageClient(new FakeBus());

    [Fact]
    public async Task SubscribeAsync_SameIdTwice_ThrowsArgumentException()
    {
        // Arrange
        var subscriberId = "subscriber-1";
        await _messageClient.SubscribeAsync<string>(subscriberId, message => Task.CompletedTask);

        // Act
        Func<Task> act = () => _messageClient.SubscribeAsync<string>(subscriberId, message => Task.CompletedTask);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task UnsubscribeAsync_ThenSubscribeAgain_DoesNotThrow()
    {
        // Arrange
        var subscriberId = "subscriber-1";
        await _messageClient.SubscribeAsync<string>(subscriberId, message => Task.CompletedTask);
        await _messageClient.UnsubscribeAsync(subscriberId);

        // Act
        Func<Task> act = () => _messageClient.SubscribeAsync<string>(subscriberId, message => Task.CompletedTask);

        // Assert
        Assert.Null(await Record.ExceptionAsync(act));
    }

    [Fact]
    public async Task UnsubscribeAsync_UnknownId_DoesNotThrow()
    {
        // Arrange
        var subscriberId = "does-not-exist";

        // Act
        Func<Task> act = () => _messageClient.UnsubscribeAsync(subscriberId);

        // Assert
        Assert.Null(await Record.ExceptionAsync(act));
    }

    [Fact]
    public async Task SubscribeAsync_ManyAtOnce_KeepsEverySubscription()
    {
        // Arrange
        // Hosted workers subscribe from background threads, so subscriptions can happen at the same time
        var subscriberIds = Enumerable.Range(0, 1000).Select(i => $"subscriber-{i}").ToList();

        // Act
        await Parallel.ForEachAsync(subscriberIds, async (id, ct) =>
            await _messageClient.SubscribeAsync<string>(id, message => Task.CompletedTask, ct));

        // Assert
        foreach (var id in subscriberIds)
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _messageClient.SubscribeAsync<string>(id, message => Task.CompletedTask));
        }
    }
}
