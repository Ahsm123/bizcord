using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Handlers;
using Bizcord.WorkspaceApi.Services;
using Xunit.Sdk;

namespace Bizcord.WorkspaceApi.Tests;

public class MessagePostedHandlerTests
{
    [Fact]
    public async Task Handle_PublishedResultEvent_WithCorrectMessageId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(new WorkspaceService(new FakeWorkspaceRepository(), client));
        var message = new MessagePostedEvent(
            MessageId: messageId,
            ChannelId: Guid.NewGuid(),
            AuthorId: Guid.NewGuid(),
            Content: "Test",
            PostedAt: DateTime.UtcNow
        );

        // Act
        await handler.Handle(message, CancellationToken.None);

        // Assert
        var published = client.SinglePublished<ChannelActivityUpdatedEvent>();
        Assert.NotNull(published);
        Assert.Equal(messageId, published.MessageId);
    }

    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        // Arrange
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(new WorkspaceService(new FakeWorkspaceRepository(), client));
        var message = new MessagePostedEvent(
            MessageId: Guid.NewGuid(),
            ChannelId: Guid.NewGuid(),
            AuthorId: Guid.NewGuid(),
            Content: "Hello world",
            PostedAt: DateTime.UtcNow
        );

        // Act
        var exception = await Record.ExceptionAsync(() => handler.Handle(message, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        Assert.Equal(1, client.publishedMessages.Count);
    }

    [Fact]
    public async Task Handle_PublishedEvent_HasLastActivityAtFromPostedAt()
    {
        // Arrange
        var postedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(new WorkspaceService(new FakeWorkspaceRepository(), client));
        var message = new MessagePostedEvent(
            MessageId: Guid.NewGuid(),
            ChannelId: Guid.NewGuid(),
            AuthorId: Guid.NewGuid(),
            Content: "Test",
            PostedAt: postedAt
        );

        // Act
        await handler.Handle(message, CancellationToken.None);

        // Assert
        var published = client.SinglePublished<ChannelActivityUpdatedEvent>();
        Assert.Equal(postedAt, published.LastActivityAt);
    }
}
