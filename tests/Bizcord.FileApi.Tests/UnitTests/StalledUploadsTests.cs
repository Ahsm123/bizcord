using Bizcord.Contracts.Commands;
using Bizcord.FileApi.Models;
using Bizcord.FileApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bizcord.FileApi.Tests.UnitTests;

public class StalledUploadsTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeUploadRepository _repository = new();
    private readonly FakeMessageClient _client = new();
    private readonly StalledUploads _job;

    public StalledUploadsTests()
    {
        _job = new StalledUploads(_repository, _client, new FixedTimeProvider(Now),
            Options.Create(new UploadSagaOptions { StalledAfter = TimeSpan.FromSeconds(30) }),
            NullLogger<StalledUploads>.Instance);
    }

    [Fact]
    public async Task StalledBeforeThePivot_RejectsAndReleasesTheQuota()
    {
        // Arrange
        var upload = Add(lastProgress: Now.AddMinutes(-1), messageRequested: false);

        // Act
        await _job.HandleAsync(CancellationToken.None);

        // Assert
        var stored = _repository.uploads[upload.Id];
        Assert.Equal(UploadStatus.Rejected, stored.Status);
        Assert.Equal(StalledUploads.TimedOutReason, stored.RejectionReason);
        Assert.Equal(new ReleaseQuota(upload.Id), Assert.Single(_client.Published<ReleaseQuota>()));
        Assert.Empty(_client.Published<PostMessage>());
    }

    [Fact]
    public async Task StalledAfterThePivot_SendsPostMessageAgainAndStaysPending()
    {
        // Arrange
        var upload = Add(lastProgress: Now.AddMinutes(-1), messageRequested: true);

        // Act
        await _job.HandleAsync(CancellationToken.None);

        // Assert
        var stored = _repository.uploads[upload.Id];
        Assert.Equal(UploadStatus.Pending, stored.Status);
        Assert.Equal(Now, stored.UpdatedAt);
        Assert.Equal(new PostMessage(upload.Id, upload.ChannelId, upload.UploaderId, upload.MessageContent()),
            Assert.Single(_client.Published<PostMessage>()));
        Assert.Empty(_client.Published<ReleaseQuota>());
    }

    [Fact]
    public async Task StalledAfterThePivot_IsNotSentAgainUntilItStallsAgain()
    {
        // Arrange
        Add(lastProgress: Now.AddMinutes(-1), messageRequested: true);

        // Act
        await _job.HandleAsync(CancellationToken.None);
        await _job.HandleAsync(CancellationToken.None);

        // Assert
        Assert.Single(_client.Published<PostMessage>());
    }

    [Fact]
    public async Task NotStalledYet_IsLeftAlone()
    {
        // Arrange
        var upload = Add(lastProgress: Now.AddSeconds(-10), messageRequested: false);

        // Act
        await _job.HandleAsync(CancellationToken.None);

        // Assert
        Assert.Equal(UploadStatus.Pending, _repository.uploads[upload.Id].Status);
        Assert.Empty(_client.publishedMessages);
    }

    [Fact]
    public async Task FinishedUpload_IsLeftAlone()
    {
        // Arrange
        var upload = Add(lastProgress: Now.AddMinutes(-1), messageRequested: true) with
        {
            Status = UploadStatus.Confirmed
        };
        _repository.uploads[upload.Id] = upload;

        // Act
        await _job.HandleAsync(CancellationToken.None);

        // Assert
        Assert.Equal(UploadStatus.Confirmed, _repository.uploads[upload.Id].Status);
        Assert.Empty(_client.publishedMessages);
    }

    private Upload Add(DateTime lastProgress, bool messageRequested)
    {
        var upload = Upload.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.pdf", 100, "Hello",
            lastProgress) with { MessageRequested = messageRequested };
        _repository.uploads[upload.Id] = upload;
        return upload;
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
