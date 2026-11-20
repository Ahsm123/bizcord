using System.Text.Json;
using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Handlers;
using Bizcord.WorkspaceApi.Services;

namespace Bizcord.WorkspaceApi.Tests;

// The handler records the activity, and the event goes to the outbox with it (#66). OutboxPublisher sends it later.
public class MessagePostedHandlerTests
{
    private readonly FakeWorkspaceRepository _repository = new();

    [Fact]
    public async Task Handle_PutsChannelActivityUpdatedInTheOutbox_WithTheMessageId()
    {
        // Arrange
        var message = NewMessage(DateTime.UtcNow);

        // Act
        await Handler().Handle(message, CancellationToken.None);

        // Assert
        var updated = SingleOutboxEvent();
        Assert.Equal(message.MessageId, updated.MessageId);
        Assert.Equal(message.ChannelId, updated.ChannelId);
    }

    [Fact]
    public async Task Handle_OutboxEvent_HasLastActivityAtFromPostedAt()
    {
        // Arrange
        var postedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await Handler().Handle(NewMessage(postedAt), CancellationToken.None);

        // Assert
        Assert.Equal(postedAt, SingleOutboxEvent().LastActivityAt);
    }

    [Fact]
    public async Task Handle_OutboxRowId_IsTheEventIdConsumersDedupeOn()
    {
        // Act
        await Handler().Handle(NewMessage(DateTime.UtcNow), CancellationToken.None);

        // Assert
        var row = Assert.Single(_repository.outbox);
        Assert.NotEqual(Guid.Empty, row.Id);
        Assert.Equal(row.Id, SingleOutboxEvent().EventId);
        Assert.Equal(typeof(ChannelActivityUpdatedEvent).FullName, row.Type);
    }

    [Fact]
    public async Task Handle_ActivityNotNewer_PutsNothingInTheOutbox()
    {
        // Arrange
        _repository.updateResult = false;

        // Act
        await Handler().Handle(NewMessage(DateTime.UtcNow), CancellationToken.None);

        // Assert
        Assert.Empty(_repository.outbox);
    }

    private MessagePostedHandler Handler() => new(new WorkspaceService(_repository));

    private ChannelActivityUpdatedEvent SingleOutboxEvent() =>
        JsonSerializer.Deserialize<ChannelActivityUpdatedEvent>(Assert.Single(_repository.outbox).Payload)!;

    private static MessagePostedEvent NewMessage(DateTime postedAt) => new(
        MessageId: Guid.NewGuid(),
        ChannelId: Guid.NewGuid(),
        AuthorId: Guid.NewGuid(),
        Content: "Test",
        PostedAt: postedAt);
}
