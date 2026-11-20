using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.FileApi.Data;
using Bizcord.FileApi.Models;
using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;

namespace Bizcord.FileApi.Services;

// Orchestrates the upload saga (docs/upload-saga.md). Every reply from Workspace and Message comes here,
// and this class decides the next step. The saga state is the upload row, found by the SagaId in the reply.
// A reply for an upload that is already Confirmed or Rejected is logged and ignored.
public class UploadSagaOrchestrator(
    IUploadRepository uploadRepository,
    IMessageClient messageClient,
    TimeProvider timeProvider,
    ILogger<UploadSagaOrchestrator> logger) :
    IMessageHandler<QuotaReservedEvent>,
    IMessageHandler<QuotaExceededEvent>,
    IMessageHandler<PostMessageSucceededEvent>,
    IMessageHandler<PostMessageFailedEvent>
{
    // Step 2 succeeded: post the message (step 3, the pivot)
    public async Task Handle(QuotaReservedEvent message, CancellationToken cancellationToken)
    {
        var upload = await uploadRepository.GetByIdAsync(message.SagaId, cancellationToken);
        if (upload is null || !await uploadRepository.MarkMessageRequestedAsync(upload.Id, Now, cancellationToken))
        {
            LogIgnored(message.SagaId, nameof(QuotaReservedEvent));
            return;
        }

        // Also sent again for a repeated QuotaReservedEvent. Message posts once per SagaId.
        await messageClient.PublishAsync(
            new PostMessage(upload.Id, upload.ChannelId, upload.UploaderId, upload.MessageContent()),
            cancellationToken);
    }

    // Step 2 failed: nothing was reserved, so there is nothing to compensate
    public async Task Handle(QuotaExceededEvent message, CancellationToken cancellationToken)
    {
        if (!await uploadRepository.CompleteAsync(message.SagaId, UploadStatus.Rejected, message.Reason, Now,
                cancellationToken))
        {
            LogIgnored(message.SagaId, nameof(QuotaExceededEvent));
        }
    }

    // Step 3 succeeded: the saga is done
    public async Task Handle(PostMessageSucceededEvent message, CancellationToken cancellationToken)
    {
        if (!await uploadRepository.CompleteAsync(message.SagaId, UploadStatus.Confirmed, null, Now,
                cancellationToken))
        {
            LogIgnored(message.SagaId, nameof(PostMessageSucceededEvent));
        }
    }

    // Step 3 failed: compensate step 2 by releasing the quota, then reject the upload
    public async Task Handle(PostMessageFailedEvent message, CancellationToken cancellationToken)
    {
        var upload = await uploadRepository.GetByIdAsync(message.SagaId, cancellationToken);
        if (upload is not { Status: UploadStatus.Pending })
        {
            LogIgnored(message.SagaId, nameof(PostMessageFailedEvent));
            return;
        }

        // Released before the upload is rejected. If rejecting fails, the retry releases again,
        // which Workspace ignores. The other order could leave the space reserved forever.
        await messageClient.PublishAsync(new ReleaseQuota(upload.Id), cancellationToken);
        await uploadRepository.CompleteAsync(upload.Id, UploadStatus.Rejected, message.Reason, Now, cancellationToken);
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private void LogIgnored(Guid sagaId, string reply) =>
        logger.LogInformation("{Reply} for upload {SagaId} ignored, the upload is unknown or already finished",
            reply, sagaId);
}
