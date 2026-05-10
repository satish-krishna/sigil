using CSharpFunctionalExtensions;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Core.Gateway;

public interface IAgentGateway
{
    /// <summary>
    /// Pre-flight validation against an agent: "can you handle this task right now?"
    /// Idempotent. Short timeout. Retried on transient failure.
    /// </summary>
    Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent,
        ValidationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Dispatch a task with its context snapshot to an agent and receive a delta.
    /// Long timeout. Retried on transient failure; not on 4xx.
    /// </summary>
    Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent,
        AgentExecutionPackage package,
        CancellationToken ct = default);
}
