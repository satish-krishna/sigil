using Sigil.Core.Identity;
using Sigil.Core.Protocol;

namespace Sigil.Core.Audit;

public sealed record AuditEntry
{
    public string AuditId { get; init; } = Guid.NewGuid().ToString("N");
    public JobId JobId { get; init; }
    public AgentId AgentId { get; init; }
    public StepId StepId { get; init; }
    public ContextDelta Delta { get; init; } = new();
    public UsageMetrics Metrics { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
