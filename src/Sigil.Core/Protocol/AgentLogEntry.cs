using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record AgentLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AgentId AgentId { get; init; }
    public string Level { get; init; } = "Info";
    public string Message { get; init; } = "";
}
