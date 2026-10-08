namespace Bizcord.Shared.Events;

public record MessagePostedEvent
(
    Guid MessageId,
    Guid ChannelId,
    Guid AuthorId,
    string Content,
    DateTime PostedAt
);