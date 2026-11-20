using Bizcord.WorkspaceApi.Data;

namespace Bizcord.WorkspaceApi.Tests;

public class FakeQuotaRepository : IQuotaRepository
{
    public ReservationOutcome NextOutcome { get; set; } = ReservationOutcome.Reserved;
    public readonly List<Guid> reserveCalls = new();
    public readonly List<Guid> releaseCalls = new();

    public Task<ReservationOutcome> ReserveAsync(Guid sagaId, Guid workspaceId, long sizeBytes, CancellationToken ct)
    {
        reserveCalls.Add(sagaId);
        return Task.FromResult(NextOutcome);
    }

    public Task<bool> ReleaseAsync(Guid sagaId, CancellationToken ct)
    {
        releaseCalls.Add(sagaId);
        return Task.FromResult(true);
    }
}
