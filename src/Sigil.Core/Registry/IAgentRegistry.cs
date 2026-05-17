using CSharpFunctionalExtensions;
using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

/// <summary>
/// Kernel-side façade over <c>IAgentRegistrationStore</c>. Owns <see cref="AgentStatus"/>
/// lifecycle rules and weighted selection for canary-style routing.
/// </summary>
public interface IAgentRegistry
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> GetAsync(AgentId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Refresh the agent's heartbeat. Promotes Starting/Degraded → Healthy; rejects Offline.
    /// </summary>
    Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default);

    Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default);

    /// <summary>
    /// Pick one Healthy agent advertising <paramref name="skillName"/>, weighted by RoutingWeight.
    /// Returns <see cref="Maybe.None"/> when no eligible candidate exists.
    /// </summary>
    Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default);
}
