using Bizcord.FileApi.Models;

namespace Bizcord.FileApi.Data;

public interface IUploadRepository
{
    Task AddAsync(Upload upload, CancellationToken ct);
    Task<Upload?> GetByIdAsync(Guid id, CancellationToken ct);
}
