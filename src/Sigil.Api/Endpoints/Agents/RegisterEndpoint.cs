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

        var result = await _registry.RegisterAsync(req, ct);
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

        var response = req with { Status = AgentStatus.Starting };
        await HttpContext.Response.SendAsync(response, StatusCodes.Status201Created, cancellation: ct);
    }
}
