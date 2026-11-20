using Bizcord.Contracts.Commands;
using Bizcord.MessageClient.Handlers;
using Bizcord.WorkspaceApi.Services;

namespace Bizcord.WorkspaceApi.Handlers;

public class ReleaseQuotaHandler(IQuotaService quotaService) : IMessageHandler<ReleaseQuota>
{
    public Task Handle(ReleaseQuota message, CancellationToken cancellationToken) =>
        quotaService.ReleaseAsync(message, cancellationToken);
}
