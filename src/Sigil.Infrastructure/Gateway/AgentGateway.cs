using System.Diagnostics;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    {
        var pre = Preflight(agent);
        if (pre.IsFailure)
            return Task.FromResult(Result.Failure<ValidationResult>(pre.Error));

        // HTTP path implemented in Task 9.
        throw new NotImplementedException();
    }

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent,
        AgentExecutionPackage package,
        CancellationToken ct = default)
    {
        var pre = Preflight(agent);
        if (pre.IsFailure)
            return Task.FromResult(Result.Failure<AgentExecutionResult>(pre.Error));

        throw new NotImplementedException();
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
