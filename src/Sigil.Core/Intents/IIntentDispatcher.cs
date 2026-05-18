using CSharpFunctionalExtensions;
using Sigil.Core.Protocol;

namespace Sigil.Core.Intents;

/// <summary>
/// Phase-1 seam between the intent endpoint and the agent gateway.
/// Phase 2 will introduce the planner-driven orchestrator behind the same interface.
/// </summary>
public interface IIntentDispatcher
{
    Task<Result<AgentExecutionResult>> DispatchAsync(
        IntentRequest request,
        CancellationToken ct = default);
}

public sealed record IntentRequest
{
    public required string SkillName { get; init; }
    public required string Input { get; init; }
    public ContextSnapshot? Snapshot { get; init; }
}
