using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;

namespace Sigil.Core.Storage;

public interface IJobStore
{
    Task<Result> CreateAsync(Job job, CancellationToken ct = default);

    Task<Maybe<Job>> GetAsync(JobId jobId, CancellationToken ct = default);

    Task<Result> UpdateStatusAsync(
        JobId jobId, JobStatus status, CancellationToken ct = default);
}
