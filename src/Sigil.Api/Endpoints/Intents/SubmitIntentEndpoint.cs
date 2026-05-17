using FastEndpoints;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;

namespace Sigil.Api.Endpoints.Intents;

public sealed record SubmitIntentRequest
{
    public required string SkillName { get; init; }
    public required string Input { get; init; }
    public ContextSnapshot? Snapshot { get; init; }
}

public sealed class SubmitIntentEndpoint : Endpoint<SubmitIntentRequest, AgentExecutionResult>
{
    private readonly IIntentDispatcher _dispatcher;
    public SubmitIntentEndpoint(IIntentDispatcher dispatcher) => _dispatcher = dispatcher;

    public override void Configure()
    {
        Post("/api/intents");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SubmitIntentRequest req, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new IntentRequest
        {
            SkillName = req.SkillName,
            Input = req.Input,
            Snapshot = req.Snapshot,
        }, ct);

        if (result.IsSuccess)
        {
            await HttpContext.Response.SendAsync(result.Value, StatusCodes.Status200OK, cancellation: ct);
            return;
        }

        var status = result.Error switch
        {
            IntentErrors.NoAgentForSkill => StatusCodes.Status404NotFound,
            _ when SigilGatewayErrorSet.All.Contains(result.Error) => StatusCodes.Status502BadGateway,
            // Everything else is a validation-side rejection (either IntentErrors.ValidationRejected
            // or a custom Reason string passed through from the agent's ValidationResult).
            _ => StatusCodes.Status400BadRequest,
        };

        HttpContext.Response.StatusCode = status;
        await HttpContext.Response.WriteAsJsonAsync(new { error = result.Error }, ct);
    }
}
