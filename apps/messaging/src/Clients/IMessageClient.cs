namespace Bizcord.Messaging.Clients;

public interface IMessageClient
{
    Task Publish<T>(T message);
    Task Subscribe<T>(string subscriberId, Func<T, Task> handler);
}