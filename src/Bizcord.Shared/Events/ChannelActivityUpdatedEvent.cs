namespace Bizcord.Shared.Events;

public class ChannelActivityUpdatedEvent
{
    public Guid MessageId { get; set; }
    public Guid ChannelId { get; set; }
    public DateTime ProcessedAt { get; set; }
}