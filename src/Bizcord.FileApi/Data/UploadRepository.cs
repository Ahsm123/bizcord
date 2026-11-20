using Bizcord.FileApi.Models;
using Dapper;
using Npgsql;

namespace Bizcord.FileApi.Data;

internal sealed class UploadRepository(NpgsqlDataSource dataSource) : IUploadRepository
{
    private const string SelectColumns = """
                                         select id, uploader_id as UploaderId, workspace_id as WorkspaceId,
                                                channel_id as ChannelId, file_name as FileName, size_bytes as SizeBytes,
                                                message, status, message_requested as MessageRequested,
                                                rejection_reason as RejectionReason, created_at as CreatedAt,
                                                updated_at as UpdatedAt
                                         from uploads
                                         """;

    public async Task AddAsync(Upload upload, CancellationToken ct)
    {
        const string sql = """
                           insert into uploads (id, uploader_id, workspace_id, channel_id, file_name, size_bytes,
                                                message, status, message_requested, rejection_reason, created_at,
                                                updated_at)
                           values (@Id, @UploaderId, @WorkspaceId, @ChannelId, @FileName, @SizeBytes, @Message,
                                   @Status, @MessageRequested, @RejectionReason, @CreatedAt, @UpdatedAt)
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, ToRow(upload), cancellationToken: ct));
    }

    public async Task<Upload?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UploadRow>(new CommandDefinition(
            $"{SelectColumns} where id = @id", new { id }, cancellationToken: ct));
        return row?.ToUpload();
    }

    private static UploadRow ToRow(Upload u) => new()
    {
        Id = u.Id, UploaderId = u.UploaderId, WorkspaceId = u.WorkspaceId, ChannelId = u.ChannelId,
        FileName = u.FileName, SizeBytes = u.SizeBytes, Message = u.Message, Status = u.Status.ToString(),
        MessageRequested = u.MessageRequested, RejectionReason = u.RejectionReason, CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };

    // The status is stored as text, so the table is readable without knowing the enum
    private sealed class UploadRow
    {
        public Guid Id { get; init; }
        public Guid UploaderId { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid ChannelId { get; init; }
        public string FileName { get; init; } = "";
        public long SizeBytes { get; init; }
        public string Message { get; init; } = "";
        public string Status { get; init; } = "";
        public bool MessageRequested { get; init; }
        public string? RejectionReason { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Upload ToUpload() => new(Id, UploaderId, WorkspaceId, ChannelId, FileName, SizeBytes, Message,
            Enum.Parse<UploadStatus>(Status), MessageRequested, RejectionReason, CreatedAt, UpdatedAt);
    }
}
