namespace Bizcord.Contracts.Events;

// Published through WorkspaceApi's outbox (#66), so a consumer can get the same event more than once.
// EventId is the same every time, so consumers use it to skip duplicates.
public record ChannelActivityUpdatedEvent(Guid MessageId, Guid ChannelId, DateTime LastActivityAt, Guid EventId);
