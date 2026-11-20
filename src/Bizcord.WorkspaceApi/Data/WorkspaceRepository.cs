using Bizcord.WorkspaceApi.Models;
using Bizcord.Contracts.Workspaces;
using Dapper;
using Npgsql;

namespace Bizcord.WorkspaceApi.Data;

internal sealed class WorkspaceRepository(NpgsqlDataSource dataSource) : IWorkspaceRepository
{
    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
                           select name, version from workspaces where id = @id;
                           select user_id as UserId, role from members where workspace_id = @id;
                           select id, name from channels where workspace_id = @id;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));

        var row = await results.ReadSingleOrDefaultAsync<WorkspaceRow>();
        if (row is null)
        {
            return null;
        }

        var members = await results.ReadAsync<Member>();
        var channels = await results.ReadAsync<Channel>();

        return Workspace.Rehydrate(id, row.Name, row.Version, members, channels);
    }

    public async Task<bool> SaveAsync(Workspace workspace, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        if (!await SaveInTransactionAsync(connection, tx, workspace, ct))
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await tx.CommitAsync(ct);
        workspace.Version++;
        return true;
    }

    public async Task<Guid> CreateAsync(Workspace workspace, Guid userId, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        if (!await SaveInTransactionAsync(connection, tx, workspace, ct))
        {
            throw new InvalidOperationException($"Workspace {workspace.Id} already exists");
        }

        // The key is saved in the same transaction as the workspace (#67). A request with the same key at the same
        // time waits here until this one commits, and then finds the key taken.
        var claimed = await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into workspace_idempotency_keys (user_id, idempotency_key, workspace_id)
            values (@userId, @idempotencyKey, @workspaceId)
            on conflict (user_id, idempotency_key) do nothing
            """,
            new { userId, idempotencyKey, workspaceId = workspace.Id }, transaction: tx, cancellationToken: ct));

        if (claimed == 0)
        {
            // Already used: undo this workspace and hand back the one the key created the first time
            var existing = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                """
                select workspace_id from workspace_idempotency_keys
                where user_id = @userId and idempotency_key = @idempotencyKey
                """,
                new { userId, idempotencyKey }, transaction: tx, cancellationToken: ct));
            await tx.RollbackAsync(ct);
            return existing;
        }

        await tx.CommitAsync(ct);
        workspace.Version++;
        return workspace.Id;
    }

    // Writes the workspace inside the caller's transaction. False when the version check fails (#68),
    // and then the caller must roll back.
    private static async Task<bool> SaveInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction tx,
        Workspace workspace, CancellationToken ct)
    {
        // Optimistic concurrency (#68): only the request that still has the current version can save, and it bumps
        // the version. The row is locked until commit, so a parallel save waits and then sees the new version.
        // A new workspace is inserted with version 1.
        var saved = await connection.ExecuteAsync(new CommandDefinition(
            workspace.Version == 0
                ? "insert into workspaces (id, name, version) values (@Id, @Name, 1) on conflict (id) do nothing"
                : "update workspaces set name = @Name, version = version + 1 where id = @Id and version = @Version",
            new { workspace.Id, workspace.Name, workspace.Version },
            transaction: tx,
            cancellationToken: ct));

        if (saved == 0)
        {
            return false;
        }

        // We need to delete the members in the db and insert them from the workspace list,
        // since dapper does not have change tracking
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from members where workspace_id = @id",
            new { id = workspace.Id },
            transaction: tx,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            "insert into members (user_id, role, workspace_id) values (@UserId, @Role, @WorkspaceId)",
            workspace.Members.Select(m => new { WorkspaceId = workspace.Id, m.UserId, Role = m.Role.ToString() }),
            transaction: tx,
            cancellationToken: ct));

        // Channels are upserted instead, so deleting them doesn't cascade away their channel_activity rows
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from channels where workspace_id = @id and not (id = any(@ids))",
            new { id = workspace.Id, ids = workspace.Channels.Select(c => c.Id).ToArray() },
            transaction: tx,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into channels (id, name, workspace_id) values (@Id, @Name, @WorkspaceId)
            on conflict (id) do update set name = excluded.name
            """,
            workspace.Channels.Select(c => new { WorkspaceId = workspace.Id, c.Id, c.Name }),
            transaction: tx,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into channel_activity (channel_id)
            select id from channels where workspace_id = @id
            on conflict (channel_id) do nothing
            """,
            new { id = workspace.Id },
            transaction: tx,
            cancellationToken: ct));

        return true;
    }

    // Read-only projection, so it skips loading members and channels for every workspace.
    // Writes load the full aggregate through GetByIdAsync instead.
    public async Task<IReadOnlyList<WorkspaceDto>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        const string sql = """
                           select w.id, w.name 
                           from workspaces w
                           join members m on m.workspace_id = w.id
                           where m.user_id = @userId
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var workspaces = await connection.QueryAsync<WorkspaceDto>(new CommandDefinition(sql, new { userId },
            cancellationToken: ct));

        return workspaces.ToList();
    }

    public async Task<bool> UpdateChannelLastActivityAsync(Guid channelId, DateTime postedAt,
        CancellationToken ct)
    {
        const string sql = """
                           update channel_activity
                           set last_activity_at = @postedAt
                           where channel_id = @channelId and last_activity_at < @postedAt;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { channelId, postedAt },
            cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
                           delete from workspaces where id = @id;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsChannelMemberAsync(Guid userId, Guid channelId, CancellationToken ct)
    {
        const string sql = """
                           select exists (
                           select 1
                           from channels c
                           join members m on m.workspace_id = c.workspace_id
                           where c.id = @channelId and m.user_id = @userId);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { channelId, userId },
            cancellationToken: ct));
    }

    private sealed class WorkspaceRow
    {
        public string Name { get; init; } = "";
        public int Version { get; init; }
    }
}
