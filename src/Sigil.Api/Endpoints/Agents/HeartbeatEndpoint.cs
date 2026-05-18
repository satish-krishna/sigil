using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed class HeartbeatEndpoint : EndpointWithoutRequest
{
    private readonly IAgentRegistry _registry;
    public HeartbeatEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Post("/api/agents/{id}/heartbeat");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = HttpContext.GetAuthenticatedAgentId();
        var id = Route<string>("id") ?? string.Empty;
        var target = new AgentId(id);
        if (caller != target)
        {
            await HttpContext.WriteSigilErrorAsync(StatusCodes.Status403Forbidden, "caller-agent-mismatch", ct);
            return;
        }

        var result = await _registry.HeartbeatAsync(target, ct);
        if (result.IsFailure)
        {
            var status = result.Error switch
            {
                RegistryErrors.AgentNotFound => StatusCodes.Status404NotFound,
                RegistryErrors.InvalidStatusTransition => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            await HttpContext.WriteSigilErrorAsync(status, result.Error, ct);
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
