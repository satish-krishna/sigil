using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using Sigil.Core.Gateway;

namespace Sigil.Infrastructure.Gateway;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AgentGatewayOptions>()
            .Bind(configuration.GetSection(AgentGatewayOptions.SectionName))
            .ValidateOnStart();

        // Typed HttpClient with two named resilience handlers — each gets its own
        // timeout + retry stack. Per-agent circuit breaking is handled separately
        // by PerAgentBreakerProvider (injected into AgentGateway).
        var httpClientBuilder = services.AddHttpClient<AgentGateway>();
        httpClientBuilder.AddResilienceHandler("agent-validate", BuildValidatePipeline);
        httpClientBuilder.AddResilienceHandler("agent-execute",  BuildExecutePipeline);

        // PerAgentBreakerProvider is registered as the concrete singleton, then
        // forwarded to the ResiliencePipelineProvider<string> abstraction so the
        // gateway can resolve it via the Polly v8 abstraction. ResiliencePipelineRegistry
        // is NOT registered — request the abstract provider type.
        services.AddSingleton<PerAgentBreakerProvider>();
        services.AddSingleton<ResiliencePipelineProvider<string>>(
            sp => sp.GetRequiredService<PerAgentBreakerProvider>());

        services.AddSingleton<IAgentGateway>(sp => sp.GetRequiredService<AgentGateway>());
        return services;
    }

    private static void BuildValidatePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext ctx)
    {
        var opts = ctx.ServiceProvider.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
        builder
            .AddTimeout(opts.ValidateTimeout)
            .AddRetry(BuildRetryOptions(opts));
    }

    private static void BuildExecutePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext ctx)
    {
        var opts = ctx.ServiceProvider.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
        builder
            .AddTimeout(opts.ExecuteTimeout)
            .AddRetry(BuildRetryOptions(opts));
    }

    private static HttpRetryStrategyOptions BuildRetryOptions(AgentGatewayOptions opts) => new()
    {
        MaxRetryAttempts = opts.MaxRetryAttempts,
        Delay            = opts.BaseRetryDelay,
        BackoffType      = DelayBackoffType.Exponential,
        UseJitter        = true,
        ShouldHandle     = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
    };

    internal static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
            return true;
        if (outcome.Result is { } response)
            return (int)response.StatusCode >= 500;
        return false;
    }
}
