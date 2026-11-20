using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Services;

namespace Bizcord.WorkspaceApi.Tests;

public class WorkspaceServiceTests
{
    [Fact]
    public async Task RecordChannelActivity_RepositoryReportsNoUpdate_PutsNothingInTheOutbox()
    {
        // Arrange
        var repository = new FakeWorkspaceRepository { updateResult = false };
        var service = new WorkspaceService(repository);
        var message = new MessagePostedEvent(
            MessageId: Guid.NewGuid(),
            ChannelId: Guid.NewGuid(),
            AuthorId: Guid.NewGuid(),
            Content: "Test",
            PostedAt: DateTime.UtcNow
        );

        // Act
        await service.RecordChannelActivityAsync(message, CancellationToken.None);

        // Assert
        Assert.Empty(repository.outbox);
    }
}
