using EasyNetQ;

namespace Bizcord.Messaging.Clients;

public class MessageClient : IMessageClient
{
    private readonly IBus _bus;
    
    public MessageClient(IBus bus)
    {
        _bus = bus;
    }
    
    public Task Publish<T>(T message)
    {
        var task = _bus.PubSub.PublishAsync(message);
        return task;
    }

    public Task Subscribe<T>(string subscriberId, Func<T, Task> handler)
    {
        var task = _bus.PubSub.SubscribeAsync(subscriberId, handler);
        return task;
    }
}