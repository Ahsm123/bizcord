namespace Bizcord.Contracts.Commands;

// File asks Message to post the upload's message in the channel (step 3, the pivot of the upload saga)
public record PostMessage(Guid SagaId, Guid ChannelId, Guid AuthorId, string Content);
