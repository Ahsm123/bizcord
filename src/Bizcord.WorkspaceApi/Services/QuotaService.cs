using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.MessageClient.Clients;
using Bizcord.WorkspaceApi.Data;

namespace Bizcord.WorkspaceApi.Services;

public interface IQuotaService
{
    Task ReserveAsync(ReserveQuota command, CancellationToken ct);
    Task ReleaseAsync(ReleaseQuota command, CancellationToken ct);
}

// Workspace's steps in the upload saga (docs/upload-saga.md). Every reply carries the saga id,
// so FileApi can match it to the upload.
public class QuotaService(IQuotaRepository quotaRepository, IMessageClient messageClient, ILogger<QuotaService> logger)
    : IQuotaService
{
    public const string NotEnoughSpace = "Not enough storage left in the workspace";
    public const string WorkspaceNotFound = "The workspace does not exist";
    public const string InvalidSize = "The size must be zero or more";

    public async Task ReserveAsync(ReserveQuota command, CancellationToken ct)
    {
        if (command.SizeBytes < 0)
        {
            await messageClient.PublishAsync(
                new QuotaExceededEvent(command.SagaId, command.WorkspaceId, InvalidSize), ct);
            return;
        }

        var outcome = await quotaRepository.ReserveAsync(command.SagaId, command.WorkspaceId, command.SizeBytes, ct);

        // A repeated command gets the same reply as the first one, in case the first reply was lost.
        // Each reply is published with its own type, because RabbitMQ routes on the message type.
        switch (outcome)
        {
            case ReservationOutcome.Reserved:
                await messageClient.PublishAsync(
                    new QuotaReservedEvent(command.SagaId, command.WorkspaceId, command.SizeBytes), ct);
                break;
            case ReservationOutcome.Exceeded:
                await messageClient.PublishAsync(
                    new QuotaExceededEvent(command.SagaId, command.WorkspaceId, NotEnoughSpace), ct);
                break;
            case ReservationOutcome.WorkspaceNotFound:
                await messageClient.PublishAsync(
                    new QuotaExceededEvent(command.SagaId, command.WorkspaceId, WorkspaceNotFound), ct);
                break;
            case ReservationOutcome.AlreadyReleased:
                // The saga has already been compensated, so FileApi doesn't need an answer
                logger.LogInformation("ReserveQuota for saga {SagaId} ignored, its quota was already released",
                    command.SagaId);
                break;
        }
    }

    public async Task ReleaseAsync(ReleaseQuota command, CancellationToken ct)
    {
        var released = await quotaRepository.ReleaseAsync(command.SagaId, ct);
        if (!released)
        {
            logger.LogInformation("ReleaseQuota for saga {SagaId} changed nothing, no space was reserved",
                command.SagaId);
        }
    }
}
