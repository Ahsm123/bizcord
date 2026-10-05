using Bizcord.Shared.Events;
using Bizcord.WorkspaceApi.Handlers;
using Xunit.Sdk;

namespace Bizcord.WorkspaceApi.Tests;

public class MessagePostedHandlerTests
{
    [Fact]
    public async Task Handle_PublishedResultEvent_WithCorrectMessageId()
    {
        var messageId = Guid.NewGuid();
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(client, new FakeWorkspaceRepository());

        await handler.Handle(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Test",
            PostedAt = DateTime.UtcNow
        }, CancellationToken.None);

        var published = client.SinglePublished<ChannelActivityUpdatedEvent>();
        Assert.NotNull(published);
        Assert.Equal(messageId, published.MessageId);
    }

    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(client, new FakeWorkspaceRepository());

        Func<Task> act = () => handler.Handle(new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.Null(await Record.ExceptionAsync(act));
        Assert.Equal(1, client.publishedMessages.Count);
    }

    [Fact]
    public async Task Handle_PublishedEvent_HasLastActivityAtFromPostedAt()
    {
        var postedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(client, new FakeWorkspaceRepository());

        await handler.Handle(new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Test",
            PostedAt = postedAt
        }, CancellationToken.None);

        var published = client.SinglePublished<ChannelActivityUpdatedEvent>();
        Assert.Equal(postedAt, published.LastActivityAt);
    }
}