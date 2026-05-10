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
        using var activity = ActivitySource.StartActivity($"agent.{method}", ActivityKind.Client);
        activity?.SetTag("sigil.agent.id", agent.AgentId.Value);
        activity?.SetTag("sigil.agent.endpoint", agent.EndpointUrl);
        activity?.SetTag("sigil.agent.tier", agent.Security.Tier.ToString());
        activity?.SetTag("sigil.gateway.method", method);
        SetTaskTags(activity, body);

        var pre = Preflight(agent);
        if (pre.IsFailure)
        {
            activity?.SetTag("sigil.gateway.error_code", pre.Error);
            activity?.SetStatus(ActivityStatusCode.Error);
            LogTerminal(_logger, pre.Error, agent, method);
            return Result.Failure<TResponse>(pre.Error);
        }

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

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            // Use the linked timeout token so a slow body read is bounded by the per-method
            // timeout. The outer catch filter classifies a CTS-fired OCE as Timeout (not
            // Cancelled) when ct itself is still live.
            var outcome = await MapResponseAsync<TResponse>(response, timeoutCts.Token).ConfigureAwait(false);
            if (outcome.IsFailure)
            {
                activity?.SetTag("sigil.gateway.error_code", outcome.Error);
                activity?.SetStatus(ActivityStatusCode.Error);
                LogTerminal(_logger, outcome.Error, agent, method);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogDebug(
                    "Gateway {Method} succeeded for {AgentId}",
                    method, agent.AgentId.Value);
            }
            return outcome;
        }
        catch (BrokenCircuitException)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.CircuitOpen, agent, method);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.Timeout, agent, method);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.Cancelled, agent, method);
        }
        catch (HttpRequestException)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.TransportError, agent, method);
        }
        catch (OperationCanceledException)
        {
            // Belt-and-suspenders: HttpClient.Timeout is Timeout.InfiniteTimeSpan and the
            // two filtered OCE catches above cover the gateway/caller token paths. Any
            // remaining OCE is a framework anomaly; surface it as Cancelled rather than
            // letting it escape and break the Result<T> contract.
            return FailWith<TResponse>(activity, SigilGatewayErrors.Cancelled, agent, method);
        }
    }

    private Result<T> FailWith<T>(Activity? activity, string code, AgentRegistration agent, string method)
    {
        activity?.SetTag("sigil.gateway.error_code", code);
        activity?.SetStatus(ActivityStatusCode.Error);
        LogTerminal(_logger, code, agent, method);
        return Result.Failure<T>(code);
    }

    private static void LogTerminal(
        ILogger logger, string code, AgentRegistration agent, string method)
    {
        switch (code)
        {
            case SigilGatewayErrors.Cancelled:
            case SigilGatewayErrors.CircuitOpen:
                logger.LogDebug(
                    "Gateway {Method} terminal {ErrorCode} for {AgentId}",
                    method, code, agent.AgentId.Value);
                break;

            case SigilGatewayErrors.AgentError:
            case SigilGatewayErrors.TransportError:
                logger.LogError(
                    "Gateway {Method} terminal {ErrorCode} for {AgentId}",
                    method, code, agent.AgentId.Value);
                break;

            default:
                // TierNotSupported, OutboundKeyMissing, EndpointInvalid,
                // AgentRejectedCredentials, AgentNotFound, AgentRejected,
                // Timeout, ProtocolError — all Warning.
                logger.LogWarning(
                    "Gateway {Method} terminal {ErrorCode} for {AgentId}",
                    method, code, agent.AgentId.Value);
                break;
        }
    }

    private static void SetTaskTags<TBody>(Activity? activity, TBody body)
    {
        if (activity is null) return;
        switch (body)
        {
            case ValidationRequest vr:
                activity.SetTag("sigil.job.id",  vr.Task.JobId.Value);
                activity.SetTag("sigil.step.id", vr.Task.StepId.Value);
                break;
            case AgentExecutionPackage pkg:
                activity.SetTag("sigil.job.id",  pkg.Task.JobId.Value);
                activity.SetTag("sigil.step.id", pkg.Task.StepId.Value);
                break;
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
        if (_security.CurrentValue.Mode != SecurityTier.Open)
            return Result.Failure<PreflightContext>(SigilGatewayErrors.TierNotSupported);

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
