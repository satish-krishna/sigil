using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Intents;

public sealed class SimpleIntentDispatcher : IIntentDispatcher
{
    private readonly IAgentRegistry _registry;
    private readonly IAgentGateway _gateway;

    public SimpleIntentDispatcher(IAgentRegistry registry, IAgentGateway gateway)
    {
        _registry = registry;
        _gateway = gateway;
    }

    public async Task<Result<AgentExecutionResult>> DispatchAsync(
        IntentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SkillName))
            return Result.Failure<AgentExecutionResult>(IntentErrors.SkillNameRequired);

        var agentMaybe = await _registry.SelectByWeightAsync(request.SkillName, ct);
        if (agentMaybe.HasNoValue)
            return Result.Failure<AgentExecutionResult>(IntentErrors.NoAgentForSkill);

        var agent = agentMaybe.Value;

        // Prefer a caller-supplied JobId from the snapshot so task/snapshot/context share one identity.
        var jobId = !string.IsNullOrEmpty(request.Snapshot?.JobId.Value)
            ? request.Snapshot.JobId
            : new JobId(Guid.NewGuid().ToString());

        var task = new AgentTask
        {
            JobId = jobId,
            SkillName = request.SkillName,
            Input = request.Input,
            AvailableTools = agent.Tools.Select(t => t.Name).ToList(),
        };

        var snapshot = request.Snapshot is null
            ? new ContextSnapshot { JobId = jobId }
            : request.Snapshot with { JobId = jobId };

        var valReq = new ValidationRequest
        {
            Task = task,
            AvailableTokenBudget = agent.MaxTokenBudget ?? 0,
        };

        var valRes = await _gateway.ValidateAsync(agent, valReq, ct);
        if (valRes.IsFailure)
            return Result.Failure<AgentExecutionResult>(valRes.Error);

        if (!valRes.Value.CanHandle)
            return Result.Failure<AgentExecutionResult>(
                valRes.Value.Reason ?? IntentErrors.ValidationRejected);

        var package = new AgentExecutionPackage
        {
            Task = task,
            Snapshot = snapshot,
            ExpectedETag = new ETag(""),
        };

        return await _gateway.ExecuteAsync(agent, package, ct);
    }
}
