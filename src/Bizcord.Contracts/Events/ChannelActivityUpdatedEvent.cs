namespace Bizcord.Contracts.Events;

public record ChannelActivityUpdatedEvent(Guid MessageId, Guid ChannelId, DateTime LastActivityAt);
