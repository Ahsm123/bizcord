using Bizcord.WorkspaceApi.Models;
using Dapper;
using Npgsql;

namespace Bizcord.WorkspaceApi.Infrastructure;

internal sealed class WorkspaceRepository(NpgsqlDataSource dataSource) : IWorkspaceRepository
{
    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
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

    public async Task SaveAsync(Workspace workspace, CancellationToken ct = default)
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
        
        // We need to delete the members and channels in the db and insert them from the workspace list,
        // since dapper does not have change tracking
        await connection.ExecuteAsync(new CommandDefinition(
            "delete from members where workspace_id = @id",
            new { id = workspace.Id },
            transaction: tx, 
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            "insert into members (user_id, role, workspace_id) values (@UserId, @Role, @WorkspaceId)",
            workspace.Members.Select(m => new { WorkspaceId = workspace.Id, m.UserId, Role = m.Role.ToString()}),
            transaction: tx,
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            "delete from channels where workspace_id = @id",
            new { id = workspace.Id },
            transaction: tx, 
            cancellationToken: ct));
        
        await connection.ExecuteAsync(new CommandDefinition(
            "insert into channels (id, name, workspace_id) values (@Id, @Name, @WorkspaceId)",
            workspace.Channels.Select(c => new { WorkspaceId = workspace.Id, c.Id, c.Name }),
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
    }
}

