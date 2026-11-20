using Dapper;
using Npgsql;

namespace Bizcord.WorkspaceApi.Data;

internal sealed class QuotaRepository(NpgsqlDataSource dataSource) : IQuotaRepository
{
    public async Task<ReservationOutcome> ReserveAsync(Guid sagaId, Guid workspaceId, long sizeBytes,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Claim the saga id first. A duplicate of the same command waits here until this transaction commits,
        // and then reads the outcome instead of reserving a second time.
        var claimed = await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into quota_reservations (saga_id, workspace_id, size_bytes, status)
            values (@sagaId, @workspaceId, @sizeBytes, 'pending')
            on conflict (saga_id) do nothing
            """,
            new { sagaId, workspaceId, sizeBytes }, transaction: tx, cancellationToken: ct));

        if (claimed == 0)
        {
            var existing = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "select status from quota_reservations where saga_id = @sagaId",
                new { sagaId }, transaction: tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return ToOutcome(existing!);
        }

        // Only succeeds when there is room. Postgres locks the row, so a parallel reservation for the same
        // workspace re-checks the condition against the used space after this one.
        var reserved = await connection.ExecuteAsync(new CommandDefinition(
            """
            update workspaces set storage_used_bytes = storage_used_bytes + @sizeBytes
            where id = @workspaceId and storage_used_bytes + @sizeBytes <= storage_limit_bytes
            """,
            new { workspaceId, sizeBytes }, transaction: tx, cancellationToken: ct));

        var status = "reserved";
        if (reserved == 0)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "select exists (select 1 from workspaces where id = @workspaceId)",
                new { workspaceId }, transaction: tx, cancellationToken: ct));
            status = exists ? "exceeded" : "not_found";
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "update quota_reservations set status = @status where saga_id = @sagaId",
            new { sagaId, status }, transaction: tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return ToOutcome(status);
    }

    public async Task<bool> ReleaseAsync(Guid sagaId, CancellationToken ct)
    {
        // One statement, so the reservation and the used space change together. Only a 'reserved' row matches,
        // so a second release, or a release for a rejected reservation, changes nothing.
        const string sql = """
                           with released as (
                               update quota_reservations set status = 'released'
                               where saga_id = @sagaId and status = 'reserved'
                               returning workspace_id, size_bytes
                           )
                           update workspaces w
                           set storage_used_bytes = w.storage_used_bytes - r.size_bytes
                           from released r
                           where w.id = r.workspace_id
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { sagaId }, cancellationToken: ct));
        return rows > 0;
    }

    private static ReservationOutcome ToOutcome(string status) => status switch
    {
        "reserved" => ReservationOutcome.Reserved,
        "exceeded" => ReservationOutcome.Exceeded,
        "not_found" => ReservationOutcome.WorkspaceNotFound,
        "released" => ReservationOutcome.AlreadyReleased,
        _ => throw new InvalidOperationException($"Unexpected reservation status '{status}'")
    };
}
