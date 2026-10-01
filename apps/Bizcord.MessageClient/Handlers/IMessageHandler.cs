namespace Bizcord.MessageClient.Handlers;

public interface IMessageHandler<in T>
{
    Task Handle(T message, CancellationToken cancellationToken);
}
