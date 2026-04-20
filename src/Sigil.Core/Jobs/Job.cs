using Sigil.Core.Identity;

namespace Sigil.Core.Jobs;

public sealed record Job
{
    public JobId JobId { get; init; }
    public JobStatus Status { get; init; } = JobStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
