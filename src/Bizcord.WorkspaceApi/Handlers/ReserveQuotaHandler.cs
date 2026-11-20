using Bizcord.Contracts.Commands;
using Bizcord.MessageClient.Handlers;
using Bizcord.WorkspaceApi.Services;

namespace Bizcord.WorkspaceApi.Handlers;

public class ReserveQuotaHandler(IQuotaService quotaService) : IMessageHandler<ReserveQuota>
{
    public Task Handle(ReserveQuota message, CancellationToken cancellationToken) =>
        quotaService.ReserveAsync(message, cancellationToken);
}
