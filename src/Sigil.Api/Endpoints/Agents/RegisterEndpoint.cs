using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed class RegisterEndpoint : Endpoint<AgentRegistration, AgentRegistration>
{
    private readonly IAgentRegistry _registry;
    public RegisterEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Post("/api/agents/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AgentRegistration req, CancellationToken ct)
    {
        var caller = HttpContext.GetAuthenticatedAgentId();
        if (caller != req.AgentId)
        {
            await HttpContext.WriteSigilErrorAsync(StatusCodes.Status403Forbidden, "caller-agent-mismatch", ct);
            return;
        }

        // Server owns lifecycle fields — overwrite any client-supplied values so they can't be spoofed.
        var now = DateTime.UtcNow;
        var normalized = req with
        {
            Status = AgentStatus.Starting,
            RegisteredAt = now,
            LastHeartbeat = now,
        };

        var result = await _registry.RegisterAsync(normalized, ct);
        if (result.IsFailure)
        {
            var status = result.Error switch
            {
                RegistryErrors.InvalidRoutingWeight => StatusCodes.Status400BadRequest,
                RegistryErrors.SkillNameRequired => StatusCodes.Status400BadRequest,
                "duplicate-agent" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            await HttpContext.WriteSigilErrorAsync(status, result.Error, ct);
            return;
        }

        await HttpContext.Response.SendAsync(normalized, StatusCodes.Status201Created, cancellation: ct);
    }
}
