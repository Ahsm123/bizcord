using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bizcord.WorkspaceApi.Tests.UnitTests;

public class QuotaServiceTests
{
    private readonly FakeQuotaRepository _repository = new();
    private readonly FakeMessageClient _client = new();
    private readonly QuotaService _service;

    public QuotaServiceTests()
    {
        _service = new QuotaService(_repository, _client, NullLogger<QuotaService>.Instance);
    }

    [Fact]
    public async Task Reserve_Reserved_PublishesQuotaReservedEvent()
    {
        // Arrange
        var command = new ReserveQuota(Guid.NewGuid(), Guid.NewGuid(), 10);

        // Act
        await _service.ReserveAsync(command, CancellationToken.None);

        // Assert
        var reply = _client.SinglePublished<QuotaReservedEvent>();
        Assert.Equal(new QuotaReservedEvent(command.SagaId, command.WorkspaceId, 10), reply);
    }

    [Theory]
    [InlineData(ReservationOutcome.Exceeded, QuotaService.NotEnoughSpace)]
    [InlineData(ReservationOutcome.WorkspaceNotFound, QuotaService.WorkspaceNotFound)]
    public async Task Reserve_Rejected_PublishesQuotaExceededEventWithReason(ReservationOutcome outcome, string reason)
    {
        // Arrange
        _repository.NextOutcome = outcome;
        var command = new ReserveQuota(Guid.NewGuid(), Guid.NewGuid(), 10);

        // Act
        await _service.ReserveAsync(command, CancellationToken.None);

        // Assert
        var reply = _client.SinglePublished<QuotaExceededEvent>();
        Assert.Equal(new QuotaExceededEvent(command.SagaId, command.WorkspaceId, reason), reply);
        Assert.Null(_client.SinglePublished<QuotaReservedEvent>());
    }

    [Fact]
    public async Task Reserve_AlreadyReleased_PublishesNothing()
    {
        // Arrange
        _repository.NextOutcome = ReservationOutcome.AlreadyReleased;

        // Act
        await _service.ReserveAsync(new ReserveQuota(Guid.NewGuid(), Guid.NewGuid(), 10), CancellationToken.None);

        // Assert
        Assert.Empty(_client.publishedMessages);
    }

    [Fact]
    public async Task Reserve_NegativeSize_IsRejectedWithoutReserving()
    {
        // Arrange
        var command = new ReserveQuota(Guid.NewGuid(), Guid.NewGuid(), -1);

        // Act
        await _service.ReserveAsync(command, CancellationToken.None);

        // Assert
        Assert.Empty(_repository.reserveCalls);
        Assert.Equal(QuotaService.InvalidSize, _client.SinglePublished<QuotaExceededEvent>()!.Reason);
    }

    [Fact]
    public async Task Release_ReleasesTheSagasReservation()
    {
        // Arrange
        var sagaId = Guid.NewGuid();

        // Act
        await _service.ReleaseAsync(new ReleaseQuota(sagaId), CancellationToken.None);

        // Assert
        Assert.Equal([sagaId], _repository.releaseCalls);
        Assert.Empty(_client.publishedMessages);
    }
}
