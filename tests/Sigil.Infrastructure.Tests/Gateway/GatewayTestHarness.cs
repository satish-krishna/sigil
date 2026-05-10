using Microsoft.Extensions.Logging.Abstractions;
using Polly.Registry;
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
}
