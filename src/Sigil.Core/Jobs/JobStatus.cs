namespace Sigil.Core.Jobs;

public enum JobStatus
{
    Pending,
    Running,
    AwaitingCheckpoint,
    Completed,
    Failed,
    Cancelled
}
