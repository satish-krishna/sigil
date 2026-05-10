using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sigil.Core.Checkpoints;
using Sigil.Core.Identity;
using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public sealed class EfCheckpointStore : ICheckpointStore
{
    private readonly SigilDbContext _ctx;
    public EfCheckpointStore(SigilDbContext ctx) => _ctx = ctx;

    public async Task<Result> CreateAsync(Checkpoint checkpoint, CancellationToken ct = default)
    {
        _ctx.Checkpoints.Add(checkpoint);
        await _ctx.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Maybe<Checkpoint>> GetAsync(string checkpointId, CancellationToken ct = default)
    {
        var found = await _ctx.Checkpoints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CheckpointId == checkpointId, ct);
        return found is null ? Maybe<Checkpoint>.None : Maybe.From(found);
    }

    public async Task<IReadOnlyList<Checkpoint>> GetPendingForJobAsync(JobId jobId, CancellationToken ct = default) =>
        await _ctx.Checkpoints.AsNoTracking()
            .Where(x => x.JobId == jobId && x.Status == CheckpointStatus.Pending)
            .ToListAsync(ct);

    public async Task<Result> ResolveAsync(
        string checkpointId,
        CheckpointStatus status,
        string resolvedBy,
        CancellationToken ct = default)
    {
        var rows = await _ctx.Checkpoints
            .Where(x => x.CheckpointId == checkpointId && x.Status == CheckpointStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.ResolvedBy, resolvedBy)
                .SetProperty(x => x.ResolvedAt, DateTime.UtcNow), ct);
        return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
    }
}
