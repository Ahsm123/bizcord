using Bizcord.Contracts.Commands;
using Bizcord.FileApi.Data;
using Microsoft.Extensions.Options;
using Bizcord.MessageClient.Clients;

namespace Bizcord.FileApi.Services;

public class UploadSagaOptions
{
    // How long a Pending upload may go without progress before the job steps in
    public TimeSpan StalledAfter { get; set; } = TimeSpan.FromSeconds(30);

    // How often the job looks for stalled uploads
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    // How many stalled uploads one check handles at most
    public int BatchSize { get; set; } = 100;
}

// Handles uploads whose saga stopped because a service never answered (docs/upload-saga.md).
// PostMessage is the pivot: before it the saga is rolled back, after it the saga is pushed forward.
public class StalledUploads(
    IUploadRepository uploadRepository,
    IMessageClient messageClient,
    TimeProvider timeProvider,
    IOptions<UploadSagaOptions> options,
    ILogger<StalledUploads> logger)
{
    public const string TimedOutReason = "Timed out waiting for the storage quota";

    public async Task HandleAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stalled = await uploadRepository.FindStalledAsync(now - options.Value.StalledAfter,
            options.Value.BatchSize, ct);

        foreach (var upload in stalled)
        {
            if (!upload.MessageRequested)
            {
                // Rejected first, and only if PostMessage still hasn't been sent, so a QuotaReservedEvent that
                // arrives right now can't end with a posted message for a rejected upload. The release is
                // safe even if ReserveQuota is still on its way: Workspace then ignores the late reserve.
                if (await uploadRepository.RejectBeforePivotAsync(upload.Id, TimedOutReason, now, ct))
                {
                    await messageClient.PublishAsync(new ReleaseQuota(upload.Id), ct);
                    logger.LogWarning("Upload {SagaId} timed out before the pivot, rejected and quota released",
                        upload.Id);
                }
            }
            else if (await uploadRepository.MarkMessageRequestedAsync(upload.Id, now, ct))
            {
                // After the pivot the message may already be posted, so go forward and ask again. Marking it
                // also restarts the clock, so the next retry waits another StalledAfter.
                await messageClient.PublishAsync(
                    new PostMessage(upload.Id, upload.ChannelId, upload.UploaderId, upload.MessageContent()), ct);
                logger.LogWarning("Upload {SagaId} timed out after the pivot, PostMessage sent again", upload.Id);
            }
        }
    }
}

// Runs StalledUploads every CheckInterval. One failed check is logged and the next one tries again.
public class StalledUploadsWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<UploadSagaOptions> options,
    TimeProvider timeProvider,
    ILogger<StalledUploadsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CheckInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<StalledUploads>().HandleAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(e, "Checking for stalled uploads failed, trying again in {Interval}",
                    options.Value.CheckInterval);
            }
        }
    }
}
