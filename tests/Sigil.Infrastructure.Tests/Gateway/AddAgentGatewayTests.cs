using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.Registry;
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Infrastructure.Gateway;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AddAgentGatewayTests
{
    private static IConfiguration BuildConfig(IDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IServiceCollection NewServices()
        => new ServiceCollection().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Resolves_IAgentGateway_As_AgentGateway()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .AddAgentGateway(config)
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<IAgentGateway>();
        resolved.ShouldBeOfType<AgentGateway>();
    }

    [Fact]
    public void GatewayOptions_Bind_From_Configuration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Gateway:ValidateTimeout"] = "00:00:03",
            ["Gateway:ExecuteTimeout"]  = "00:01:00",
            ["Gateway:MaxRetryAttempts"] = "5"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .AddAgentGateway(config)
            .BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptionsMonitor<AgentGatewayOptions>>().CurrentValue;
        opts.ValidateTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        opts.ExecuteTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        opts.MaxRetryAttempts.ShouldBe(5);
    }

    [Fact]
    public void HttpClient_Has_Infinite_Timeout_So_Gateway_Manages_Timeout_Itself()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .AddAgentGateway(config)
            .BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var http = factory.CreateClient(typeof(AgentGateway).Name);

        http.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void Registers_ResiliencePipelineProvider_For_PerAgent_Breakers()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .AddAgentGateway(config)
            .BuildServiceProvider();

        var registry = provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        registry.ShouldNotBeNull();

        // Two distinct agents get distinct pipelines (per-agent isolation).
        var pipelineA = registry.GetPipeline<HttpResponseMessage>("agent-circuit::agent-a");
        var pipelineB = registry.GetPipeline<HttpResponseMessage>("agent-circuit::agent-b");
        pipelineA.ShouldNotBeNull();
        pipelineB.ShouldNotBeNull();
        pipelineA.ShouldNotBeSameAs(pipelineB);
    }
}
