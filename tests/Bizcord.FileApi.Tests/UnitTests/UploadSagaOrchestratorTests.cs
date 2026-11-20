using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.FileApi.Models;
using Bizcord.FileApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bizcord.FileApi.Tests.UnitTests;

public class UploadSagaOrchestratorTests
{
    private readonly FakeUploadRepository _repository = new();
    private readonly FakeMessageClient _client = new();
    private readonly UploadSagaOrchestrator _saga;
    private readonly Upload _upload;

    public UploadSagaOrchestratorTests()
    {
        _saga = new UploadSagaOrchestrator(_repository, _client, TimeProvider.System,
            NullLogger<UploadSagaOrchestrator>.Instance);
        _upload = Upload.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.pdf", 100, "Hello",
            DateTime.UtcNow);
        _repository.uploads[_upload.Id] = _upload;
    }

    private Upload Stored => _repository.uploads[_upload.Id];

    [Fact]
    public async Task HappyPath_QuotaReservedThenMessagePosted_ConfirmsTheUpload()
    {
        // Act
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);
        await _saga.Handle(new PostMessageSucceededEvent(_upload.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        var command = Assert.Single(_client.Published<PostMessage>());
        Assert.Equal(new PostMessage(_upload.Id, _upload.ChannelId, _upload.UploaderId, "Hello\n[file] report.pdf"),
            command);
        Assert.Equal(UploadStatus.Confirmed, Stored.Status);
        Assert.True(Stored.MessageRequested);
        Assert.Empty(_client.Published<ReleaseQuota>());
    }

    [Fact]
    public async Task QuotaExceeded_RejectsWithTheReasonAndCompensatesNothing()
    {
        // Act
        await _saga.Handle(new QuotaExceededEvent(_upload.Id, _upload.WorkspaceId, "Full"), CancellationToken.None);

        // Assert
        Assert.Equal(UploadStatus.Rejected, Stored.Status);
        Assert.Equal("Full", Stored.RejectionReason);
        Assert.Empty(_client.publishedMessages);
    }

    [Fact]
    public async Task PostMessageFailed_ReleasesTheQuotaAndRejects()
    {
        // Arrange
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);

        // Act
        await _saga.Handle(new PostMessageFailedEvent(_upload.Id, "Down"), CancellationToken.None);

        // Assert
        Assert.Equal(new ReleaseQuota(_upload.Id), Assert.Single(_client.Published<ReleaseQuota>()));
        Assert.Equal(UploadStatus.Rejected, Stored.Status);
        Assert.Equal("Down", Stored.RejectionReason);
    }

    [Fact]
    public async Task LateReplies_AfterTheUploadIsRejected_AreIgnored()
    {
        // Arrange
        await _saga.Handle(new QuotaExceededEvent(_upload.Id, _upload.WorkspaceId, "Full"), CancellationToken.None);

        // Act
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);
        await _saga.Handle(new PostMessageSucceededEvent(_upload.Id, Guid.NewGuid()), CancellationToken.None);
        await _saga.Handle(new PostMessageFailedEvent(_upload.Id, "Down"), CancellationToken.None);

        // Assert
        Assert.Equal(UploadStatus.Rejected, Stored.Status);
        Assert.Equal("Full", Stored.RejectionReason);
        Assert.Empty(_client.publishedMessages);
    }

    [Fact]
    public async Task LateFailure_AfterTheUploadIsConfirmed_IsIgnored()
    {
        // Arrange
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);
        await _saga.Handle(new PostMessageSucceededEvent(_upload.Id, Guid.NewGuid()), CancellationToken.None);

        // Act
        await _saga.Handle(new PostMessageFailedEvent(_upload.Id, "Down"), CancellationToken.None);

        // Assert
        Assert.Equal(UploadStatus.Confirmed, Stored.Status);
        Assert.Empty(_client.Published<ReleaseQuota>());
    }

    [Fact]
    public async Task RepeatedQuotaReserved_WhilePending_SendsPostMessageAgain()
    {
        // Act
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);
        await _saga.Handle(new QuotaReservedEvent(_upload.Id, _upload.WorkspaceId, 100), CancellationToken.None);

        // Assert: Message posts once per SagaId, so sending it twice is safe, and it covers a lost first send
        Assert.Equal(2, _client.Published<PostMessage>().Count);
        Assert.Equal(UploadStatus.Pending, Stored.Status);
    }

    [Fact]
    public async Task ReplyForUnknownUpload_IsIgnored()
    {
        // Act
        await _saga.Handle(new QuotaReservedEvent(Guid.NewGuid(), Guid.NewGuid(), 1), CancellationToken.None);
        await _saga.Handle(new PostMessageFailedEvent(Guid.NewGuid(), "Down"), CancellationToken.None);

        // Assert
        Assert.Empty(_client.publishedMessages);
    }

    [Fact]
    public void MessageContent_WithoutText_IsOnlyTheFile()
    {
        // Arrange
        var upload = Upload.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "a.png", 1, null, DateTime.UtcNow);

        // Act + Assert
        Assert.Equal("[file] a.png", upload.MessageContent());
    }
}
