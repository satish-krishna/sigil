using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record AgentExecutionPackage
{
    public required AgentTask Task { get; init; }
    public required ContextSnapshot Snapshot { get; init; }
    public required ETag ExpectedETag { get; init; }
}
