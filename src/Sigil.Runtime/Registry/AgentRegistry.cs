using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Storage;

namespace Sigil.Runtime.Registry;

/// <summary>
/// Default <see cref="IAgentRegistry"/> implementation. Wraps <see cref="IAgentRegistrationStore"/>
/// and enforces status-transition rules and weighted selection.
/// </summary>
public sealed class AgentRegistry : IAgentRegistry
{
    private readonly IAgentRegistrationStore _store;
    private readonly IRandomProvider _random;

    public AgentRegistry(IAgentRegistrationStore store, IRandomProvider random)
    {
        _store = store;
        _random = random;
    }

    public Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
    {
        if (registration.RoutingWeight < 0 || registration.RoutingWeight > 100)
            return Task.FromResult(Result.Failure(RegistryErrors.InvalidRoutingWeight));

        var normalized = registration with { Status = AgentStatus.Starting };
        return _store.RegisterAsync(normalized, ct);
    }

    public Task<Maybe<AgentRegistration>> GetAsync(AgentId id, CancellationToken ct = default)
        => _store.GetAsync(id, ct);

    public Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default)
        => _store.GetAllAsync(ct);

    public Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
        => _store.FindBySkillAsync(skillName, ct);

    public Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default)
        => _store.FindByDomainAsync(domain, ct);

    public Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default)
        => throw new NotImplementedException();
}
