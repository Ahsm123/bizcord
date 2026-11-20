using Dapper;
using Npgsql;

namespace Bizcord.WorkspaceApi.Outbox;

public interface IOutboxRepository
{
    // Hands the oldest unsent messages to publish one at a time and marks each one sent only when publish returned.
    // Stops at the first failure, so the rest stay unsent and are tried again next time. Returns how many were sent.
    Task<int> PublishPendingAsync(Func<OutboxMessage, Task> publish, int batchSize, CancellationToken ct);

    // Removes sent messages older than the cutoff. Unsent messages are never removed.
    Task<int> DeleteSentBeforeAsync(DateTime cutoff, CancellationToken ct);
}

internal sealed class OutboxRepository(NpgsqlDataSource dataSource) : IOutboxRepository
{
    // Called by the repositories inside their own transaction, so the change and its event commit together
    public static Task AddAsync(NpgsqlConnection connection, NpgsqlTransaction tx, OutboxMessage message,
        CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(
            "insert into outbox (id, type, payload) values (@Id, @Type, @Payload::jsonb)",
            message, transaction: tx, cancellationToken: ct));

    public async Task<int> PublishPendingAsync(Func<OutboxMessage, Task> publish, int batchSize,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // skip locked: a second WorkspaceApi instance takes the next rows instead of sending the same ones
        var pending = await connection.QueryAsync<OutboxMessage>(new CommandDefinition(
            """
            select id, type, payload::text as payload from outbox
            where sent_at is null
            order by created_at
            limit @batchSize
            for update skip locked
            """,
            new { batchSize }, transaction: tx, cancellationToken: ct));

        var sent = 0;
        try
        {
            foreach (var message in pending)
            {
                await publish(message);
                await connection.ExecuteAsync(new CommandDefinition(
                    "update outbox set sent_at = now() where id = @Id",
                    new { message.Id }, transaction: tx, cancellationToken: ct));
                sent++;
            }
        }
        finally
        {
            // The ones published before a failure stay marked as sent
            await tx.CommitAsync(CancellationToken.None);
        }

        return sent;
    }

    public async Task<int> DeleteSentBeforeAsync(DateTime cutoff, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            "delete from outbox where sent_at < @cutoff", new { cutoff }, cancellationToken: ct));
    }
}
