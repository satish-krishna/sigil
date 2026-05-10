using Microsoft.EntityFrameworkCore;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public sealed class EfAuditStore : IAuditStore
{
    private readonly SigilDbContext _ctx;
    public EfAuditStore(SigilDbContext ctx) => _ctx = ctx;

    public async Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _ctx.AuditEntries.Add(entry);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(JobId jobId, CancellationToken ct = default) =>
        await _ctx.AuditEntries.AsNoTracking()
            .Where(x => x.JobId == jobId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(AgentId agentId, CancellationToken ct = default) =>
        await _ctx.AuditEntries.AsNoTracking()
            .Where(x => x.AgentId == agentId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync(ct);
}
