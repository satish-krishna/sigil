using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Registry;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;

namespace Sigil.Infrastructure.Gateway;

public sealed class AgentGateway : IAgentGateway
{
    public static readonly ActivitySource ActivitySource = new("Sigil.Gateway", "1.0.0");

    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<SigilSecurityOptions> _security;
    private readonly ResiliencePipelineProvider<string> _breakers;
    private readonly IOptionsMonitor<AgentGatewayOptions> _gatewayOptions;
    private readonly ILogger<AgentGateway> _logger;

    public AgentGateway(
        HttpClient http,
        IOptionsMonitor<SigilSecurityOptions> security,
        ResiliencePipelineProvider<string> breakers,
        IOptionsMonitor<AgentGatewayOptions> gatewayOptions,
        ILogger<AgentGateway> logger)
    {
        _http = http;
        _security = security;
        _breakers = breakers;
        _gatewayOptions = gatewayOptions;
        _logger = logger;
    }

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent,
        ValidationRequest request,
        CancellationToken ct = default)
        => DispatchAsync<ValidationRequest, ValidationResult>(
            agent, request, subPath: "/sigil/validate", method: "validate", ct);

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent,
        AgentExecutionPackage package,
        CancellationToken ct = default)
        => DispatchAsync<AgentExecutionPackage, AgentExecutionResult>(
            agent, package, subPath: "/sigil/execute", method: "execute", ct);

    private async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(
        AgentRegistration agent, TRequest body, string subPath, string method, CancellationToken ct)
    {
        var pre = Preflight(agent);
        if (pre.IsFailure)
            return Result.Failure<TResponse>(pre.Error);

        _logger.LogDebug("Gateway dispatch {Method} for {AgentId}", method, agent.AgentId.Value);

        var requestUri = ComposeEndpoint(pre.Value.BaseUri, subPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("X-Sigil-Key", pre.Value.OutboundKey);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        var opts = _gatewayOptions.CurrentValue;
        var timeout = method == "validate" ? opts.ValidateTimeout : opts.ExecuteTimeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var breaker = _breakers.GetPipeline<HttpResponseMessage>(
            $"agent-circuit::{agent.AgentId.Value}");

        try
        {
            using var response = await breaker.ExecuteAsync(
                static async (state, ct) =>
                    await state.Http.SendAsync(state.Request, ct).ConfigureAwait(false),
                (Http: _http, Request: request),
                timeoutCts.Token).ConfigureAwait(false);

            // Pass the caller's ct so a long-running body read can be cancelled by the
            // caller giving up. If the per-method timeout fires while the body is
            // being read, the linked CTS surfaces an OperationCanceledException that
            // the outer catch filter classifies as Timeout (not Cancelled).
            return await MapResponseAsync<TResponse>(response, ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.CircuitOpen);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // The per-method timeout fired; the caller's token is still live.
            return Result.Failure<TResponse>(SigilGatewayErrors.Timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.Cancelled);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.TransportError);
        }
    }

    private static Uri ComposeEndpoint(Uri baseUri, string subPath)
    {
        var basePath = baseUri.AbsoluteUri.TrimEnd('/');
        return new Uri(basePath + subPath, UriKind.Absolute);
    }

    private static async Task<Result<TResponse>> MapResponseAsync<TResponse>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;

        if (status >= 200 && status < 300)
        {
            try
            {
                var deserialized = await response.Content
                    .ReadFromJsonAsync<TResponse>(JsonOptions, ct)
                    .ConfigureAwait(false);
                if (deserialized is null)
                    return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
                return Result.Success(deserialized);
            }
            catch (JsonException)
            {
                return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
            }
            catch (NotSupportedException)
            {
                return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
            }
        }

        return status switch
        {
            401 or 403 => Result.Failure<TResponse>(SigilGatewayErrors.AgentRejectedCredentials),
            404        => Result.Failure<TResponse>(SigilGatewayErrors.AgentNotFound),
            >= 400 and < 500 => Result.Failure<TResponse>(SigilGatewayErrors.AgentRejected),
            // 5xx (and any other unmapped code, e.g., 1xx/3xx that HttpClient surfaces directly): treat as agent error.
            _ => Result.Failure<TResponse>(SigilGatewayErrors.AgentError),
        };
    }

    private Result<PreflightContext> Preflight(AgentRegistration agent)
    {
        if (agent.Security.Tier != SecurityTier.Open)
            return Result.Failure<PreflightContext>(SigilGatewayErrors.TierNotSupported);

        if (string.IsNullOrWhiteSpace(agent.EndpointUrl)
            || !Uri.TryCreate(agent.EndpointUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<PreflightContext>(SigilGatewayErrors.EndpointInvalid);
        }

        var keys = _security.CurrentValue.OpenTier.Keys;
        if (!keys.TryGetValue(agent.AgentId.Value, out var outboundKey))
            return Result.Failure<PreflightContext>(SigilGatewayErrors.OutboundKeyMissing);

        return Result.Success(new PreflightContext(baseUri, outboundKey));
    }

    private readonly record struct PreflightContext(Uri BaseUri, string OutboundKey);

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new AgentIdJsonConverter());
        options.Converters.Add(new JobIdJsonConverter());
        options.Converters.Add(new StepIdJsonConverter());
        options.Converters.Add(new ETagJsonConverter());
        return options;
    }
}
