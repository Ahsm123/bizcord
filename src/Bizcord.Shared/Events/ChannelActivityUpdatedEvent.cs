namespace Bizcord.Shared.Events;

public record ChannelActivityUpdatedEvent(Guid MessageId, Guid ChannelId, DateTime LastActivityAt);
