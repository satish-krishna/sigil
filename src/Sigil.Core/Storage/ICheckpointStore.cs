using CSharpFunctionalExtensions;
using Sigil.Core.Checkpoints;
using Sigil.Core.Identity;

namespace Sigil.Core.Storage;

public interface ICheckpointStore
{
    Task<Result> CreateAsync(Checkpoint checkpoint, CancellationToken ct = default);

    Task<Maybe<Checkpoint>> GetAsync(string checkpointId, CancellationToken ct = default);

    Task<IReadOnlyList<Checkpoint>> GetPendingForJobAsync(
        JobId jobId, CancellationToken ct = default);

    Task<Result> ResolveAsync(
        string checkpointId,
        CheckpointStatus status,
        string resolvedBy,
        CancellationToken ct = default);
}
