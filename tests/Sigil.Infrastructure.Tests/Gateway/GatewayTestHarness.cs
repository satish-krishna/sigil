using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Registry;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Security;
using Sigil.Infrastructure.Gateway;
using Sigil.Infrastructure.Security;
using Sigil.Infrastructure.Tests.Security;

namespace Sigil.Infrastructure.Tests.Gateway;

internal static class GatewayTestHarness
{
    /// <summary>
    /// Builds an AgentGateway around a raw HttpClient that wraps the provided
    /// FakeHttpMessageHandler — bypasses the DI-registered resilience handlers.
    /// Use for tests that assert request shape, header values, body serialization,
    /// HTTP outcome mapping, and pre-flight checks.
    /// </summary>
    public static AgentGateway WithRawClient(
        FakeHttpMessageHandler handler,
        SigilSecurityOptions? security = null,
        AgentGatewayOptions? gateway = null)
    {
        security ??= new SigilSecurityOptions { Mode = SecurityTier.Open };
        gateway ??= new AgentGatewayOptions();

        var http = new HttpClient(handler);

        var securityMonitor = new TestOptionsMonitor<SigilSecurityOptions>(security);

        // No-op breaker registry: returns a do-nothing pipeline. Resilience tests
        // (Task 13) use the DI-built harness with real per-agent breakers instead.
        var registry = new ResiliencePipelineRegistry<string>();

        return new AgentGateway(
            http,
            securityMonitor,
            registry,
            new TestOptionsMonitor<AgentGatewayOptions>(gateway),
            NullLogger<AgentGateway>.Instance);
    }

    public static SigilSecurityOptions OpenWithKey(string agentId, string key)
    {
        var opts = new SigilSecurityOptions { Mode = SecurityTier.Open };
        opts.OpenTier.Keys[agentId] = key;
        return opts;
    }

    public static AgentRegistration MakeRegistration(
        string agentId = "echo-agent",
        string endpointUrl = "http://echo-agent:8080",
        SecurityTier tier = SecurityTier.Open)
    {
        return new AgentRegistration
        {
            AgentId = new AgentId(agentId),
            Name = agentId,
            Domain = "test",
            EndpointUrl = endpointUrl,
            Model = new ModelSpec { Provider = "test", Model = "test-model" },
            Security = new SecurityProfile { Tier = tier }
        };
    }

    /// <summary>
    /// Builds an IAgentGateway via the full DI pipeline (AddSigilSecurity + AddAgentGateway),
    /// with the FakeHttpMessageHandler injected as the primary handler. The named resilience
    /// handlers and per-agent breaker registry are active.
    /// </summary>
    public static (IAgentGateway Gateway, ServiceProvider Provider) WithResilience(
        FakeHttpMessageHandler handler,
        SigilSecurityOptions? security = null,
        AgentGatewayOptions? gateway = null)
    {
        security ??= new SigilSecurityOptions { Mode = SecurityTier.Open };
        gateway  ??= new AgentGatewayOptions();

        var configValues = new Dictionary<string, string?>
        {
            ["Security:Mode"] = security.Mode.ToString(),
            ["Gateway:ValidateTimeout"] = gateway.ValidateTimeout.ToString(),
            ["Gateway:ExecuteTimeout"]  = gateway.ExecuteTimeout.ToString(),
            ["Gateway:MaxRetryAttempts"] = gateway.MaxRetryAttempts.ToString(),
            ["Gateway:BaseRetryDelay"]   = gateway.BaseRetryDelay.ToString(),
            ["Gateway:CircuitBreakerFailureRatio"]      = gateway.CircuitBreakerFailureRatio.ToString(),
            ["Gateway:CircuitBreakerMinimumThroughput"] = gateway.CircuitBreakerMinimumThroughput.ToString(),
            ["Gateway:CircuitBreakerSamplingDuration"]  = gateway.CircuitBreakerSamplingDuration.ToString(),
            ["Gateway:CircuitBreakerBreakDuration"]     = gateway.CircuitBreakerBreakDuration.ToString(),
        };
        foreach (var (id, key) in security.OpenTier.Keys)
            configValues[$"Security:OpenTier:Keys:{id}"] = key;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSigilSecurity(configuration);
        services.AddAgentGateway(configuration);

        // Replace the primary handler on the AgentGateway typed-client with the fake.
        services.AddHttpClient<AgentGateway>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IAgentGateway>();
        return (resolved, provider);
    }
}
