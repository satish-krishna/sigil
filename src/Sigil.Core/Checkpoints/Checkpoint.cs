using Sigil.Core.Identity;

namespace Sigil.Core.Checkpoints;

public sealed record Checkpoint
{
    public string CheckpointId { get; init; } = Guid.NewGuid().ToString("N");
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public CheckpointStatus Status { get; init; } = CheckpointStatus.Pending;
    public string? ResolvedBy { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; init; }
}
