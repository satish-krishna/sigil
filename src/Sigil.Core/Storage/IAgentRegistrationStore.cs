using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Core.Storage;

public interface IAgentRegistrationStore
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(
        string capabilityName, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(
        string domain, CancellationToken ct = default);

    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);

    Task<Result> UpdateStatusAsync(
        AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
