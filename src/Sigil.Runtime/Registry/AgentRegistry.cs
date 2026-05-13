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

    public async Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default)
    {
        var maybe = await _store.GetAsync(id, ct);
        if (maybe.HasNoValue)
            return Result.Failure(RegistryErrors.AgentNotFound);

        var current = maybe.Value.Status;
        if (current == AgentStatus.Offline)
            return Result.Failure(RegistryErrors.InvalidStatusTransition);

        var beat = await _store.UpdateHeartbeatAsync(id, ct);
        if (beat.IsFailure)
            return beat;

        if (current is AgentStatus.Starting or AgentStatus.Degraded)
            return await _store.UpdateStatusAsync(id, AgentStatus.Healthy, ct);

        return Result.Success();
    }

    public Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default)
        => TransitionAsync(id, AgentStatus.Healthy, ct);

    public Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default)
        => TransitionAsync(id, AgentStatus.Degraded, ct);

    public Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default)
        => TransitionAsync(id, AgentStatus.Offline, ct);

    public Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default)
        => TransitionAsync(id, AgentStatus.Draining, ct);

    public Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default)
        => throw new NotImplementedException();

    private async Task<Result> TransitionAsync(AgentId id, AgentStatus target, CancellationToken ct)
    {
        var maybe = await _store.GetAsync(id, ct);
        if (maybe.HasNoValue)
            return Result.Failure(RegistryErrors.AgentNotFound);

        if (!IsLegalTransition(maybe.Value.Status, target))
            return Result.Failure(RegistryErrors.InvalidStatusTransition);

        return await _store.UpdateStatusAsync(id, target, ct);
    }

    private static bool IsLegalTransition(AgentStatus from, AgentStatus to) => (from, to) switch
    {
        (AgentStatus.Starting, AgentStatus.Healthy)  => true,
        (AgentStatus.Starting, AgentStatus.Offline)  => true,

        (AgentStatus.Healthy,  AgentStatus.Degraded) => true,
        (AgentStatus.Healthy,  AgentStatus.Offline)  => true,
        (AgentStatus.Healthy,  AgentStatus.Draining) => true,

        (AgentStatus.Degraded, AgentStatus.Healthy)  => true,
        (AgentStatus.Degraded, AgentStatus.Offline)  => true,
        (AgentStatus.Degraded, AgentStatus.Draining) => true,

        (AgentStatus.Draining, AgentStatus.Offline)  => true,

        _ => false
    };
}
