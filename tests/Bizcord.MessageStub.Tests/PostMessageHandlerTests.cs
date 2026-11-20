using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.MessageStub.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bizcord.MessageStub.Tests;

public class PostMessageHandlerTests
{
    private readonly FakeMessageClient _client = new();
    private readonly PostedMessages _postedMessages = new();
    private readonly PostMessage _command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.pdf");

    [Fact]
    public async Task Succeed_PublishesMessagePostedAndRepliesWithTheSameMessageId()
    {
        // Act
        await HandlerWith(StubBehavior.Succeed).Handle(_command, CancellationToken.None);

        // Assert
        var posted = Assert.Single(_client.Published<MessagePostedEvent>());
        var reply = Assert.Single(_client.Published<PostMessageSucceededEvent>());
        Assert.Equal(_command.SagaId, reply.SagaId);
        Assert.Equal(posted.MessageId, reply.MessageId);
        Assert.Equal((_command.ChannelId, _command.AuthorId, _command.Content),
            (posted.ChannelId, posted.AuthorId, posted.Content));
    }

    [Fact]
    public async Task Succeed_SameSagaTwice_PostsOnceAndRepliesBothTimes()
    {
        // Arrange
        var handler = HandlerWith(StubBehavior.Succeed);

        // Act
        await handler.Handle(_command, CancellationToken.None);
        await handler.Handle(_command, CancellationToken.None);

        // Assert
        Assert.Single(_client.Published<MessagePostedEvent>());
        var replies = _client.Published<PostMessageSucceededEvent>();
        Assert.Equal(2, replies.Count);
        Assert.Equal(replies[0], replies[1]);
    }

    [Fact]
    public async Task Succeed_PublishFailsThenRetried_PostsTheMessageOnRetry()
    {
        // Arrange
        var handler = HandlerWith(StubBehavior.Succeed);
        _client.FailNextPublishOf = typeof(MessagePostedEvent);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(_command, CancellationToken.None));
        await handler.Handle(_command, CancellationToken.None);

        // Assert
        Assert.Single(_client.Published<MessagePostedEvent>());
        Assert.Single(_client.Published<PostMessageSucceededEvent>());
    }

    [Fact]
    public async Task Fail_RepliesPostMessageFailedAndPostsNothing()
    {
        // Act
        await HandlerWith(StubBehavior.Fail).Handle(_command, CancellationToken.None);

        // Assert
        var reply = Assert.Single(_client.Published<PostMessageFailedEvent>());
        Assert.Equal(new PostMessageFailedEvent(_command.SagaId, PostMessageHandler.FailReason), reply);
        Assert.Empty(_client.Published<MessagePostedEvent>());
    }

    [Fact]
    public async Task NoReply_PublishesNothing()
    {
        // Act
        await HandlerWith(StubBehavior.NoReply).Handle(_command, CancellationToken.None);

        // Assert
        Assert.Empty(_client.publishedMessages);
    }

    private PostMessageHandler HandlerWith(StubBehavior behavior)
    {
        var options = new StaticOptionsMonitor(new MessageStubOptions { Behavior = behavior });
        return new PostMessageHandler(_postedMessages, _client, options, TimeProvider.System,
            NullLogger<PostMessageHandler>.Instance);
    }

    private sealed class StaticOptionsMonitor(MessageStubOptions value) : IOptionsMonitor<MessageStubOptions>
    {
        public MessageStubOptions CurrentValue => value;
        public MessageStubOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<MessageStubOptions, string?> listener) => null;
    }
}
