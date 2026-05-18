using System.Text.Json;

namespace Sigil.Api.Security;

internal static class HttpResponseErrorExtensions
{
    /// <summary>
    /// Writes a JSON error body of the form <c>{ "error": "&lt;code&gt;" }</c> using
    /// <see cref="JsonSerializer"/> directly. We bypass <c>WriteAsJsonAsync</c> because
    /// FastEndpoints' DI-resolved <c>JsonSerializerOptions</c> includes a source-generated
    /// serializer context that throws on anonymous types.
    /// </summary>
    public static Task WriteSigilErrorAsync(this HttpContext ctx, int statusCode, string code, CancellationToken ct = default)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = code }), ct);
    }
}
