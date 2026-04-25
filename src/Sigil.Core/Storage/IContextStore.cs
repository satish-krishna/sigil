using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;

namespace Sigil.Core.Storage;

public interface IContextStore
{
    Task<Result<(ContextSnapshot Snapshot, ETag ETag)>> GetSnapshotAsync(
        JobId jobId,
        CancellationToken ct = default);

    Task<Result> CommitDeltaAsync(
        JobId jobId,
        ContextDelta delta,
        ETag expectedETag,
        CancellationToken ct = default);

    Task AppendLogAsync(
        JobId jobId,
        AgentLogEntry entry,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentLogEntry>> GetLogAsync(
        JobId jobId,
        CancellationToken ct = default);
}
