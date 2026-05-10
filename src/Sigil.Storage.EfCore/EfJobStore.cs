using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;
using Sigil.Core.Protocol;
using Sigil.Core.Storage;
using Sigil.Storage.EfCore.Persistence;

namespace Sigil.Storage.EfCore;

public sealed class EfJobStore : IJobStore
{
    private readonly SigilDbContext _ctx;
    public EfJobStore(SigilDbContext ctx) => _ctx = ctx;

    public async Task<Result> CreateAsync(Job job, CancellationToken ct = default)
    {
        var existing = await _ctx.Jobs.FindAsync(new object?[] { job.JobId }, ct);
        if (existing is not null) return Result.Failure(StorageErrors.NotFound);

        _ctx.Jobs.Add(job);
        _ctx.ContextStates.Add(new ContextStateRecord
        {
            JobId = job.JobId.Value,
            ETag = Guid.NewGuid().ToString("N"),
            State = new Dictionary<string, object>(),
            Log = new List<AgentLogEntry>()
        });
        await _ctx.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Maybe<Job>> GetAsync(JobId jobId, CancellationToken ct = default)
    {
        var found = await _ctx.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == jobId, ct);
        return found is null ? Maybe<Job>.None : Maybe.From(found);
    }

    public async Task<Result> UpdateStatusAsync(JobId jobId, JobStatus status, CancellationToken ct = default)
    {
        var rows = await _ctx.Jobs
            .Where(x => x.JobId == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
    }
}
