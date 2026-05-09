namespace Sigil.Core.Protocol;

public sealed record ValidationRequest
{
    public required AgentTask Task { get; init; }
    public int AvailableTokenBudget { get; init; }
}
