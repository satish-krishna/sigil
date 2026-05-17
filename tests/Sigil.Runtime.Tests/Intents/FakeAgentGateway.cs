using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Intents;

internal sealed class FakeAgentGateway : IAgentGateway
{
    public Func<AgentRegistration, ValidationRequest, Result<ValidationResult>> OnValidate { get; set; } =
        (_, _) => Result.Success(new ValidationResult { CanHandle = true });

    public Func<AgentRegistration, AgentExecutionPackage, Result<AgentExecutionResult>> OnExecute { get; set; } =
        (_, _) => Result.Success(new AgentExecutionResult { Delta = new ContextDelta() });

    public List<(AgentRegistration Agent, ValidationRequest Request)> ValidateCalls { get; } = new();
    public List<(AgentRegistration Agent, AgentExecutionPackage Package)> ExecuteCalls { get; } = new();

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default)
    {
        ValidateCalls.Add((agent, request));
        return Task.FromResult(OnValidate(agent, request));
    }

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default)
    {
        ExecuteCalls.Add((agent, package));
        return Task.FromResult(OnExecute(agent, package));
    }
}
