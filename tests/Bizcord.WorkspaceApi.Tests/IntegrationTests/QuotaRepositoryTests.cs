using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// Runs the SQL against the real Postgres from compose, since the atomic update is what keeps parallel
// reservations from overbooking the quota. A fake repository can't show that.
public class QuotaRepositoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _app = factory.WithWebHostBuilder(b => b
        .UseSetting("ConnectionStrings:Messaging", "host=localhost;username=kalo;password=kalo")
        .UseSetting("Jwt:IssuerSigningKey", "test-signing-key-that-is-at-least-32-bytes-long"));

    [Fact]
    public async Task Reserve_WithinLimit_IsReservedAndCounted()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);

        // Act
        var outcome = await ReserveAsync(Guid.NewGuid(), workspaceId, 60);

        // Assert
        Assert.Equal(ReservationOutcome.Reserved, outcome);
        Assert.Equal(60, await UsedBytesAsync(workspaceId));
    }

    [Fact]
    public async Task Reserve_OverLimit_IsExceededAndNothingIsCounted()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);

        // Act
        var outcome = await ReserveAsync(Guid.NewGuid(), workspaceId, 101);

        // Assert
        Assert.Equal(ReservationOutcome.Exceeded, outcome);
        Assert.Equal(0, await UsedBytesAsync(workspaceId));
    }

    [Fact]
    public async Task Reserve_UnknownWorkspace_IsWorkspaceNotFound()
    {
        // Act
        var outcome = await ReserveAsync(Guid.NewGuid(), Guid.NewGuid(), 1);

        // Assert
        Assert.Equal(ReservationOutcome.WorkspaceNotFound, outcome);
    }

    [Fact]
    public async Task Reserve_TwoInParallelThatTogetherExceedTheLimit_OnlyOneIsReserved()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);

        // Act
        var outcomes = await Task.WhenAll(
            ReserveAsync(Guid.NewGuid(), workspaceId, 60),
            ReserveAsync(Guid.NewGuid(), workspaceId, 60));

        // Assert
        Assert.Single(outcomes, o => o == ReservationOutcome.Reserved);
        Assert.Single(outcomes, o => o == ReservationOutcome.Exceeded);
        Assert.Equal(60, await UsedBytesAsync(workspaceId));
    }

    [Fact]
    public async Task Reserve_SameSagaTwice_ReservesOnceAndRepeatsTheOutcome()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);
        var sagaId = Guid.NewGuid();

        // Act
        var first = await ReserveAsync(sagaId, workspaceId, 60);
        var second = await ReserveAsync(sagaId, workspaceId, 60);

        // Assert
        Assert.Equal(ReservationOutcome.Reserved, first);
        Assert.Equal(ReservationOutcome.Reserved, second);
        Assert.Equal(60, await UsedBytesAsync(workspaceId));
    }

    [Fact]
    public async Task Reserve_SameSagaTwiceInParallel_ReservesOnce()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);
        var sagaId = Guid.NewGuid();

        // Act
        var outcomes = await Task.WhenAll(
            ReserveAsync(sagaId, workspaceId, 60),
            ReserveAsync(sagaId, workspaceId, 60));

        // Assert
        Assert.All(outcomes, o => Assert.Equal(ReservationOutcome.Reserved, o));
        Assert.Equal(60, await UsedBytesAsync(workspaceId));
    }

    [Fact]
    public async Task Release_GivesTheSpaceBackOnce()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);
        var sagaId = Guid.NewGuid();
        await ReserveAsync(sagaId, workspaceId, 60);

        // Act
        var first = await ReleaseAsync(sagaId);
        var second = await ReleaseAsync(sagaId);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(0, await UsedBytesAsync(workspaceId));
        Assert.Equal(ReservationOutcome.AlreadyReleased, await ReserveAsync(sagaId, workspaceId, 60));
    }

    [Fact]
    public async Task Release_RejectedReservation_ChangesNothing()
    {
        // Arrange
        var workspaceId = await CreateWorkspaceAsync(limitBytes: 100);
        await ReserveAsync(Guid.NewGuid(), workspaceId, 50);
        var rejected = Guid.NewGuid();
        await ReserveAsync(rejected, workspaceId, 80);

        // Act
        var released = await ReleaseAsync(rejected);

        // Assert
        Assert.False(released);
        Assert.Equal(50, await UsedBytesAsync(workspaceId));
    }

    private async Task<Guid> CreateWorkspaceAsync(long limitBytes)
    {
        var workspace = Workspace.Create(Guid.NewGuid(), $"quota-test-{Guid.NewGuid()}");
        using (var scope = _app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
                .SaveAsync(workspace, CancellationToken.None);
        }

        await using var connection = await _app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        await connection.ExecuteAsync("update workspaces set storage_limit_bytes = @limitBytes where id = @id",
            new { limitBytes, id = workspace.Id });
        return workspace.Id;
    }

    // Each call gets its own scope, like two messages handled at the same time
    private async Task<ReservationOutcome> ReserveAsync(Guid sagaId, Guid workspaceId, long sizeBytes)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQuotaRepository>()
            .ReserveAsync(sagaId, workspaceId, sizeBytes, CancellationToken.None);
    }

    private async Task<bool> ReleaseAsync(Guid sagaId)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQuotaRepository>()
            .ReleaseAsync(sagaId, CancellationToken.None);
    }

    private async Task<long> UsedBytesAsync(Guid workspaceId)
    {
        await using var connection = await _app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "select storage_used_bytes from workspaces where id = @workspaceId", new { workspaceId });
    }
}
