using FastEndpoints;

namespace Sigil.Api.Agents;

/// <summary>
/// GET /api/agents — placeholder scaffold.
/// Full implementation lands in Task 15.
/// </summary>
public sealed class ListAgentsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/agents");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
}
