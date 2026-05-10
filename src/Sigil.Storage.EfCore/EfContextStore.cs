using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public sealed class EfContextStore : IContextStore
{
    private readonly SigilDbContext _ctx;
    public EfContextStore(SigilDbContext ctx) => _ctx = ctx;

    public async Task<Result<(ContextSnapshot Snapshot, ETag ETag)>> GetSnapshotAsync(
        JobId jobId, CancellationToken ct = default)
    {
        var row = await _ctx.ContextStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
        if (row is null)
            return Result.Failure<(ContextSnapshot, ETag)>(StorageErrors.NotFound);

        var snapshot = new ContextSnapshot { JobId = jobId, State = row.State };
        return Result.Success((snapshot, new ETag(row.ETag)));
    }

    public async Task<Result> CommitDeltaAsync(
        JobId jobId,
        ContextDelta delta,
        ETag expectedETag,
        CancellationToken ct = default)
    {
        // Read current state for merge. AsNoTracking — we'll write via ExecuteUpdateAsync.
        var row = await _ctx.ContextStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
        if (row is null) return Result.Failure(StorageErrors.NotFound);
        if (row.ETag != expectedETag.Value) return Result.Failure(StorageErrors.EtagMismatch);

        var nextState = ApplyDelta(row.State, delta);
        var nextETag = Guid.NewGuid().ToString("N");

        // Single atomic UPDATE ... WHERE etag = @expected — row-level lock prevents races.
        var rows = await _ctx.ContextStates
            .Where(x => x.JobId == jobId.Value && x.ETag == expectedETag.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.State, nextState)
                .SetProperty(x => x.ETag, nextETag), ct);

        return rows == 1
            ? Result.Success()
            : Result.Failure(StorageErrors.EtagMismatch);
    }

    public async Task AppendLogAsync(JobId jobId, AgentLogEntry entry, CancellationToken ct = default)
    {
        // Serializable transaction: read-modify-write on the log list so concurrent appends
        // don't overwrite each other.
        await using var tx = await _ctx.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var row = await _ctx.ContextStates
            .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
        if (row is null) throw new InvalidOperationException(
            $"No context state for job {jobId.Value}; was JobStore.CreateAsync called?");

        row.Log = new List<AgentLogEntry>(row.Log) { entry };
        await _ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AgentLogEntry>> GetLogAsync(JobId jobId, CancellationToken ct = default)
    {
        var row = await _ctx.ContextStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
        return row is null ? Array.Empty<AgentLogEntry>() : row.Log;
    }

    private static IReadOnlyDictionary<string, object> ApplyDelta(
        IReadOnlyDictionary<string, object> current, ContextDelta delta)
    {
        var merged = new Dictionary<string, object>(current);
        foreach (var k in delta.Removals) merged.Remove(k);
        foreach (var (k, v) in delta.Updates) merged[k] = v;
        return merged;
    }
}
