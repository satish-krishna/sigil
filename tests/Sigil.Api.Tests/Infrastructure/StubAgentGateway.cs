using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Infrastructure;

public sealed class StubAgentGateway : IAgentGateway
{
    public Func<AgentRegistration, ValidationRequest, Result<ValidationResult>> OnValidate { get; set; } =
        (_, _) => Result.Success(new ValidationResult { CanHandle = true });

    public Func<AgentRegistration, AgentExecutionPackage, Result<AgentExecutionResult>> OnExecute { get; set; } =
        (_, _) => Result.Success(new AgentExecutionResult { Delta = new ContextDelta() });

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default) =>
        Task.FromResult(OnValidate(agent, request));

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default) =>
        Task.FromResult(OnExecute(agent, package));
}
