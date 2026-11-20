using Bizcord.FileApi.Data;
using Bizcord.FileApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcord.FileApi.Tests.IntegrationTests;

// Runs the SQL against the file-db from compose (localhost:5434), since the conditional updates are what stop
// a late reply or the timeout job from moving an upload that another message already finished
public class UploadRepositoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // The timeout job is pushed far out, so it can't change an upload in the middle of a test
    private readonly WebApplicationFactory<Program> _app = factory.WithWebHostBuilder(b => b
        .UseSetting("Jwt:IssuerSigningKey", "test-signing-key-that-is-at-least-32-bytes-long")
        .UseSetting("UploadSaga:CheckInterval", "01:00:00"));

    [Fact]
    public async Task AddThenGet_ReturnsTheSameUpload()
    {
        // Arrange
        var upload = NewUpload();

        // Act
        await WithRepository(r => r.AddAsync(upload, CancellationToken.None));
        var stored = await WithRepository(r => r.GetByIdAsync(upload.Id, CancellationToken.None));

        // Assert
        Assert.Equal(upload with { CreatedAt = stored!.CreatedAt, UpdatedAt = stored.UpdatedAt }, stored);
    }

    [Fact]
    public async Task Complete_OnlyTheFirstOneMovesThePendingUpload()
    {
        // Arrange
        var upload = await AddAsync();

        // Act
        var confirmed = await Complete(upload.Id, UploadStatus.Confirmed);
        var rejected = await Complete(upload.Id, UploadStatus.Rejected, "late");

        // Assert
        Assert.True(confirmed);
        Assert.False(rejected);
        Assert.Equal(UploadStatus.Confirmed, (await Get(upload.Id))!.Status);
    }

    [Fact]
    public async Task Complete_TwoAtTheSameTime_ExactlyOneWins()
    {
        // Arrange
        var upload = await AddAsync();

        // Act
        var results = await Task.WhenAll(
            Complete(upload.Id, UploadStatus.Confirmed),
            Complete(upload.Id, UploadStatus.Rejected, "timeout"));

        // Assert
        Assert.Single(results, r => r);
    }

    [Fact]
    public async Task MarkMessageRequested_PendingUpload_IsMarked()
    {
        // Arrange
        var upload = await AddAsync();

        // Act
        var marked = await WithRepository(r => r.MarkMessageRequestedAsync(upload.Id, DateTime.UtcNow, default));

        // Assert
        Assert.True(marked);
        Assert.True((await Get(upload.Id))!.MessageRequested);
    }

    [Fact]
    public async Task MarkMessageRequested_RejectedUpload_IsNotMarked()
    {
        // Arrange
        var upload = await AddAsync();
        await Complete(upload.Id, UploadStatus.Rejected, "full");

        // Act
        var marked = await WithRepository(r => r.MarkMessageRequestedAsync(upload.Id, DateTime.UtcNow, default));

        // Assert
        Assert.False(marked);
        Assert.False((await Get(upload.Id))!.MessageRequested);
    }

    // Created now, so no running timeout job sees it as stalled during the test
    [Fact]
    public async Task FindStalled_ReturnsOnlyPendingUploadsOlderThanTheCutoff()
    {
        // Arrange: far in the future, so no running timeout job touches them before the assert
        var cutoff = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var stalled = await AddAsync(NewUpload(cutoff.AddMinutes(-5)));
        var recent = await AddAsync(NewUpload(cutoff.AddMinutes(5)));
        var finished = await AddAsync(NewUpload(cutoff.AddMinutes(-5)));
        await Complete(finished.Id, UploadStatus.Confirmed);

        // Act
        var found = await WithRepository(r => r.FindStalledAsync(cutoff, 1000, CancellationToken.None));

        // Assert
        var ids = found.Select(u => u.Id).ToList();
        Assert.Contains(stalled.Id, ids);
        Assert.DoesNotContain(recent.Id, ids);
        Assert.DoesNotContain(finished.Id, ids);
    }

    [Fact]
    public async Task RejectBeforePivot_BeforePostMessage_Rejects()
    {
        // Arrange
        var upload = await AddAsync();

        // Act
        var rejected = await WithRepository(r => r.RejectBeforePivotAsync(upload.Id, "timeout", DateTime.UtcNow, default));

        // Assert
        Assert.True(rejected);
        Assert.Equal(UploadStatus.Rejected, (await Get(upload.Id))!.Status);
    }

    [Fact]
    public async Task RejectBeforePivot_AfterPostMessage_LeavesItPending()
    {
        // Arrange
        var upload = await AddAsync();
        await WithRepository(r => r.MarkMessageRequestedAsync(upload.Id, DateTime.UtcNow, default));

        // Act
        var rejected = await WithRepository(r => r.RejectBeforePivotAsync(upload.Id, "timeout", DateTime.UtcNow, default));

        // Assert
        Assert.False(rejected);
        Assert.Equal(UploadStatus.Pending, (await Get(upload.Id))!.Status);
    }

    private static Upload NewUpload(DateTime? now = null) =>
        Upload.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "report.pdf", 100, "Hello",
            now ?? DateTime.UtcNow);

    private async Task<Upload> AddAsync(Upload? upload = null)
    {
        upload ??= NewUpload();
        await WithRepository(r => r.AddAsync(upload, CancellationToken.None));
        return upload;
    }

    private Task<Upload?> Get(Guid id) => WithRepository(r => r.GetByIdAsync(id, CancellationToken.None));

    private Task<bool> Complete(Guid id, UploadStatus status, string? reason = null) =>
        WithRepository(r => r.CompleteAsync(id, status, reason, DateTime.UtcNow, CancellationToken.None));

    // Each call gets its own scope, like two messages handled at the same time
    private async Task<T> WithRepository<T>(Func<IUploadRepository, Task<T>> action)
    {
        using var scope = _app.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IUploadRepository>());
    }

    private async Task WithRepository(Func<IUploadRepository, Task> action)
    {
        using var scope = _app.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<IUploadRepository>());
    }
}
