using Bizcord.MessageClient.Handlers;
using Bizcord.Shared.Events;
using Bizcord.WorkspaceApi.Services;

namespace Bizcord.WorkspaceApi.Handlers;

public class MessagePostedHandler(IWorkspaceService workspaceService) : IMessageHandler<MessagePostedEvent>
{
    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        await workspaceService.RecordChannelActivityAsync(message, cancellationToken);
    }
}
