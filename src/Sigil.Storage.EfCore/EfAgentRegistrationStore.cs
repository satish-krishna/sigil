using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public sealed class EfAgentRegistrationStore : IAgentRegistrationStore
{
    private readonly SigilDbContext _ctx;
    public EfAgentRegistrationStore(SigilDbContext ctx) => _ctx = ctx;

    public async Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
    {
        var validation = Validate(registration);
        if (validation.IsFailure) return validation;

        var existing = await _ctx.AgentRegistrations.FindAsync(new object?[] { registration.AgentId }, ct);
        if (existing is not null) return Result.Failure(StorageErrors.DuplicateAgent);

        _ctx.AgentRegistrations.Add(registration);
        await _ctx.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default)
    {
        var found = await _ctx.AgentRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentId == agentId, ct);
        return found is null ? Maybe<AgentRegistration>.None : Maybe.From(found);
    }

    public async Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default) =>
        await _ctx.AgentRegistrations.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
    {
        // Pull all then filter in-memory: works on every provider, doesn't depend on
        // jsonb-specific operators. The GIN index from the migration optimizes the
        // future server-side variant; for v1 traffic levels (a handful of agents),
        // in-memory filtering is fine. Replace with a server-side jsonb_path query
        // if registry size grows.
        var all = await _ctx.AgentRegistrations.AsNoTracking().ToListAsync(ct);
        return all.Where(a => a.Skills.Any(s => s.Name == skillName)).ToList();
    }

    public async Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default) =>
        await _ctx.AgentRegistrations.AsNoTracking()
            .Where(x => x.Domain == domain)
            .ToListAsync(ct);

    public async Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default)
    {
        var rows = await _ctx.AgentRegistrations
            .Where(x => x.AgentId == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastHeartbeat, DateTime.UtcNow), ct);
        return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
    }

    public async Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default)
    {
        var rows = await _ctx.AgentRegistrations
            .Where(x => x.AgentId == agentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), ct);
        return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
    }

    private static Result Validate(AgentRegistration reg)
    {
        if (reg.Skills.Any(s => string.IsNullOrWhiteSpace(s.Name)))
            return Result.Failure(StorageErrors.ValidationSkillName);

        var skillNames = reg.Skills.Select(s => s.Name).ToList();
        if (skillNames.Distinct(StringComparer.Ordinal).Count() != skillNames.Count)
            return Result.Failure(StorageErrors.ValidationSkillDuplicate);

        var toolNames = reg.Tools.Select(t => t.Name).ToList();
        if (toolNames.Distinct(StringComparer.Ordinal).Count() != toolNames.Count)
            return Result.Failure(StorageErrors.ValidationToolNameDuplicate);

        var toolSet = toolNames.ToHashSet(StringComparer.Ordinal);
        foreach (var skill in reg.Skills)
            foreach (var required in skill.RequiredTools)
                if (!toolSet.Contains(required))
                    return Result.Failure(StorageErrors.ValidationSkillRequiresUnknownTool);

        foreach (var allowed in reg.Security.AllowedTools)
            if (!toolSet.Contains(allowed))
                return Result.Failure(StorageErrors.ValidationAllowedToolUnknown);

        return Result.Success();
    }
}
