using Sigil.Core.Audit;
using Sigil.Core.Identity;

namespace Sigil.Core.Storage;

public interface IAuditStore
{
    Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
        JobId jobId, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(
        AgentId agentId, CancellationToken ct = default);
}
