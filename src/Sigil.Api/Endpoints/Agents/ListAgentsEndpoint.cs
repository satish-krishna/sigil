using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed record ListAgentsRequest
{
    public string? Skill { get; init; }
    public string? Domain { get; init; }
}

public sealed class ListAgentsEndpoint : Endpoint<ListAgentsRequest, IReadOnlyList<AgentRegistration>>
{
    private readonly IAgentRegistry _registry;
    public ListAgentsEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Get("/api/agents");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListAgentsRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.Skill) && !string.IsNullOrWhiteSpace(req.Domain))
        {
            await HttpContext.WriteSigilErrorAsync(StatusCodes.Status400BadRequest, "conflicting-filters", ct);
            return;
        }

        IReadOnlyList<AgentRegistration> result =
            !string.IsNullOrWhiteSpace(req.Skill) ? await _registry.FindBySkillAsync(req.Skill, ct)
            : !string.IsNullOrWhiteSpace(req.Domain) ? await _registry.FindByDomainAsync(req.Domain, ct)
            : await _registry.GetAllAsync(ct);

        await HttpContext.Response.SendAsync(result, StatusCodes.Status200OK, cancellation: ct);
    }
}
