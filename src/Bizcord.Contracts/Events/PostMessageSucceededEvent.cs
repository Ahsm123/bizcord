namespace Bizcord.Contracts.Events;

// The reply to PostMessage. Message also publishes MessagePostedEvent for everyone else.
public record PostMessageSucceededEvent(Guid SagaId, Guid MessageId);
