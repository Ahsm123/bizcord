namespace Bizcord.Contracts.Events;

public record PostMessageFailedEvent(Guid SagaId, string Reason);
