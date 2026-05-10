using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
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

        // One named resilience handler — retry only. Timeouts are NOT applied here;
        // attaching multiple named handlers to the same typed HttpClient chains them
        // sequentially so every request runs ALL handlers, multiplying retry attempts.
        // Per-method timeouts (validate: 5 s, execute: 120 s) are applied by the gateway
        // itself via CancellationTokenSource.CancelAfter linked to the caller's token.
        // Per-agent circuit breaking is handled separately by PerAgentBreakerProvider.
        var httpClientBuilder = services.AddHttpClient<AgentGateway>(client =>
        {
            // Gateway owns dispatch timeout via CancellationTokenSource.CancelAfter linked
            // to the caller's token. Default HttpClient.Timeout (100s) is less than the
            // default ExecuteTimeout (120s) and would fire with a token matching neither
            // of our catch filters — let the gateway-managed CTS be the single source of truth.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        httpClientBuilder.AddResilienceHandler("agent-retry", BuildRetryPipeline);

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

    private static void BuildRetryPipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext ctx)
    {
        var opts = ctx.ServiceProvider.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
        builder.AddRetry(BuildRetryOptions(opts));
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
        if (outcome.Exception is HttpRequestException)
            return true;
        if (outcome.Result is { } response)
            return (int)response.StatusCode >= 500;
        return false;
    }
}
