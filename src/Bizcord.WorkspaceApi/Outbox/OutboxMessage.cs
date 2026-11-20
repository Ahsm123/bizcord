using System.Text.Json;

namespace Bizcord.WorkspaceApi.Outbox;

// An event waiting in the outbox table. Type is the event's full type name, so the publisher can send it with its own
// type again, which RabbitMQ routes on.
public record OutboxMessage(Guid Id, string Type, string Payload)
{
    public static OutboxMessage For<T>(Guid eventId, T message) =>
        new(eventId, typeof(T).FullName!, JsonSerializer.Serialize(message));
}
