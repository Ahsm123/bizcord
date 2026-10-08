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
                           select name from workspaces where id = @id;
                           select user_id as UserId, role from members where workspace_id = @id;
                           select id, name from channels where workspace_id = @id;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { id }, cancellationToken: ct));

        var name = await results.ReadSingleOrDefaultAsync<string>();
        if (name is null)
        {
            return null;
        }

        var members = await results.ReadAsync<Member>();
        var channels = await results.ReadAsync<Channel>();

        return Workspace.Rehydrate(id, name, members, channels);
    }

    public async Task SaveAsync(Workspace workspace, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into workspaces (id, name) values (@Id, @Name)
            on conflict (id) do update set name = excluded.name
            """,
            new { workspace.Id, workspace.Name },
            transaction: tx,
            cancellationToken: ct));

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

        await tx.CommitAsync(ct);
    }

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
}