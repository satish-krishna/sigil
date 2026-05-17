using System.Text.Json;
using Sigil.Core.Identity;
using Sigil.Core.Security;

namespace Sigil.Api.Security;

public sealed class SigilAuthMiddleware
{
    public const string HeaderAgentId = "X-Sigil-Agent-Id";
    public const string HeaderKey = "X-Sigil-Key";
    public const string AuthContextItemKey = "sigil.auth";
    public const string ErrorMissingCredentials = "missing-credentials";
    public const string ErrorInvalidAgentId = "invalid-agent-id";

    private readonly RequestDelegate _next;

    public SigilAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ISigilSecurity security)
    {
        var agentIdHeader = ctx.Request.Headers[HeaderAgentId].ToString();
        var keyHeader = ctx.Request.Headers[HeaderKey].ToString();

        if (string.IsNullOrWhiteSpace(agentIdHeader) || string.IsNullOrWhiteSpace(keyHeader))
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, ErrorMissingCredentials);
            return;
        }

        if (agentIdHeader.Length > 256)
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, ErrorInvalidAgentId);
            return;
        }

        var credentials = new SigilCredentials
        {
            AgentId = new AgentId(agentIdHeader),
            SigilKey = keyHeader,
        };

        var auth = await security.AuthenticateAsync(credentials, SecurityTier.Open, ctx.RequestAborted);
        if (auth.IsFailure)
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, auth.Error);
            return;
        }

        ctx.Items[AuthContextItemKey] = auth.Value;
        await _next(ctx);
    }

    private static async Task WriteError(HttpContext ctx, int statusCode, string code)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = code }));
    }
}
