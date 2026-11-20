using System.Text.Json;
using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceApi.Outbox;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// #66 against the real Postgres: the activity and its event are written together, and an event is only marked as
// sent once publishing succeeded. The app's own publisher is pushed far out, so the test decides when to publish.
[Collection(IntegrationCollection.Name)]
public class OutboxTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _app = factory.WithWebHostBuilder(b => b
        .UseSetting("ConnectionStrings:Messaging", "host=localhost;username=kalo;password=kalo")
        .UseSetting("Jwt:IssuerSigningKey", "test-signing-key-that-is-at-least-32-bytes-long")
        .UseSetting("Outbox:PollInterval", "01:00:00"));

    // Stops this test's host, so it doesn't keep consuming RabbitMQ messages meant for the next test
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _app.DisposeAsync().AsTask();


    [Fact]
    public async Task NewerActivity_WritesTheEventToTheOutboxUnsent()
    {
        // Arrange
        var channelId = await CreateChannelAsync();
        var message = Event(channelId);

        // Act
        var updated = await UpdateActivityAsync(channelId, DateTime.UtcNow.AddMinutes(1), message);

        // Assert
        Assert.True(updated);
        var row = await OutboxRowAsync(message.Id);
        Assert.NotNull(row);
        Assert.Null(row.SentAt);
    }

    [Fact]
    public async Task OlderActivity_WritesNothingToTheOutbox()
    {
        // Arrange
        var channelId = await CreateChannelAsync();
        var message = Event(channelId);

        // Act: older than the channel's initial now(), so the guard doesn't update
        var updated = await UpdateActivityAsync(channelId, DateTime.UtcNow.AddDays(-1), message);

        // Assert
        Assert.False(updated);
        Assert.Null(await OutboxRowAsync(message.Id));
    }

    [Fact]
    public async Task BrokerDown_TheEventStaysUnsentAndGoesOutWhenItIsBack()
    {
        // Arrange
        var channelId = await CreateChannelAsync();
        var message = Event(channelId);
        await UpdateActivityAsync(channelId, DateTime.UtcNow.AddMinutes(1), message);
        var client = new FakeMessageClient { brokerDown = true };

        // Act 1: RabbitMQ is down
        await Assert.ThrowsAnyAsync<Exception>(() => DispatchAsync(client));

        // Assert 1
        Assert.Null((await OutboxRowAsync(message.Id))!.SentAt);

        // Act 2: RabbitMQ is back
        client.brokerDown = false;
        await DispatchAsync(client);

        // Assert 2: sent with its own type and marked as sent
        Assert.NotNull((await OutboxRowAsync(message.Id))!.SentAt);
        var index = client.publishedMessages.FindIndex(m => m is ChannelActivityUpdatedEvent e && e.EventId == message.Id);
        Assert.True(index >= 0);
        Assert.Equal(typeof(ChannelActivityUpdatedEvent), client.publishedTypes[index]);
    }

    [Fact]
    public async Task SentEventsOlderThanKeepSentFor_AreDeletedButUnsentAreKept()
    {
        // Arrange
        var oldSent = Event(Guid.NewGuid());
        var oldUnsent = Event(Guid.NewGuid());
        await using (var connection = await DataSource.OpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                """
                insert into outbox (id, type, payload, created_at, sent_at) values
                    (@sent, 'test', '{}'::jsonb, now() - interval '3 hours', now() - interval '2 hours'),
                    (@unsent, 'test', '{}'::jsonb, now() - interval '3 hours', null)
                """,
                new { sent = oldSent.Id, unsent = oldUnsent.Id });
        }

        // Act
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxRepository>()
            .DeleteSentBeforeAsync(DateTime.UtcNow.AddHours(-1), CancellationToken.None);

        // Assert
        Assert.Null(await OutboxRowAsync(oldSent.Id));
        Assert.NotNull(await OutboxRowAsync(oldUnsent.Id));

        // Clean up, so the unsent test row doesn't wait in the outbox forever
        await using var cleanup = await DataSource.OpenConnectionAsync();
        await cleanup.ExecuteAsync("delete from outbox where id = @id", new { id = oldUnsent.Id });
    }

    private NpgsqlDataSource DataSource => _app.Services.GetRequiredService<NpgsqlDataSource>();

    private static OutboxMessage Event(Guid channelId)
    {
        var e = new ChannelActivityUpdatedEvent(Guid.NewGuid(), channelId, DateTime.UtcNow, Guid.NewGuid());
        return OutboxMessage.For(e.EventId, e);
    }

    private async Task<Guid> CreateChannelAsync()
    {
        var owner = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var workspace = Workspace.Create(owner, $"outbox-{Guid.NewGuid()}");
        workspace.CreateChannel(owner, channelId, "general");
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().SaveAsync(workspace, CancellationToken.None);
        return channelId;
    }

    private async Task<bool> UpdateActivityAsync(Guid channelId, DateTime postedAt, OutboxMessage message)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
            .UpdateChannelLastActivityAsync(channelId, postedAt, message, CancellationToken.None);
    }

    // A dispatcher with the given client, so the test controls whether RabbitMQ is up
    private async Task DispatchAsync(FakeMessageClient client)
    {
        using var scope = _app.Services.CreateScope();
        var dispatcher = new OutboxDispatcher(scope.ServiceProvider.GetRequiredService<IOutboxRepository>(), client,
            TimeProvider.System, Options.Create(new OutboxOptions { BatchSize = 1000 }));
        await dispatcher.DispatchAsync(CancellationToken.None);
    }

    private async Task<OutboxRow?> OutboxRowAsync(Guid id)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<OutboxRow>(
            "select id, sent_at as SentAt from outbox where id = @id", new { id });
    }

    private sealed class OutboxRow
    {
        public Guid Id { get; init; }
        public DateTime? SentAt { get; init; }
    }
}
