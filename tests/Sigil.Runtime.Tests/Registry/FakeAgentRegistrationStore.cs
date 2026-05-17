using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Storage;

namespace Sigil.Runtime.Tests.Registry;

/// <summary>
/// Minimal in-memory <see cref="IAgentRegistrationStore"/> for unit tests.
/// No concurrency control; tests are single-threaded.
/// </summary>
internal sealed class FakeAgentRegistrationStore : IAgentRegistrationStore
{
    private readonly Dictionary<AgentId, AgentRegistration> _items = new();

    public IReadOnlyDictionary<AgentId, AgentRegistration> Snapshot => _items;

    public Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
    {
        // Mirror production EfAgentRegistrationStore: reject duplicate AgentId with the
        // exact same error string ("duplicate-agent") used by Sigil.Storage.EfCore.StorageErrors.DuplicateAgent.
        // The runtime test project cannot reference the EfCore project, so the literal is duplicated by design.
        if (_items.ContainsKey(registration.AgentId))
            return Task.FromResult(Result.Failure("duplicate-agent"));
        _items[registration.AgentId] = registration;
        return Task.FromResult(Result.Success());
    }

    public Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(agentId, out var v) ? Maybe.From(v) : Maybe<AgentRegistration>.None);

    public Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(_items.Values.ToList());

    public Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(
            _items.Values.Where(a => a.Skills.Any(s => s.Name == skillName)).ToList());

    public Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(
            _items.Values.Where(a => a.Domain == domain).ToList());

    public Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(agentId, out var existing))
            return Task.FromResult(Result.Failure(RegistryErrors.AgentNotFound));
        _items[agentId] = existing with { LastHeartbeat = DateTime.UtcNow };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(agentId, out var existing))
            return Task.FromResult(Result.Failure(RegistryErrors.AgentNotFound));
        _items[agentId] = existing with { Status = status };
        return Task.FromResult(Result.Success());
    }
}
