using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace Sigil.Infrastructure.Gateway;

/// <summary>
/// A <see cref="ResiliencePipelineProvider{TKey}"/> that lazily creates one circuit-breaker
/// pipeline per agent key. Pipelines are cached after first access — each agent gets its own
/// independent breaker state so a flapping agent cannot trip other agents' breakers.
/// </summary>
internal sealed class PerAgentBreakerProvider : ResiliencePipelineProvider<string>
{
    // Bounded in practice by the number of registered agents. The kernel controls
    // all call sites; arbitrary-key injection is not a concern in the current threat model.
    private readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> _typed = new();
    private readonly AgentGatewayOptions _opts;

    public PerAgentBreakerProvider(IOptions<AgentGatewayOptions> opts)
        => _opts = opts.Value;

    public override ResiliencePipeline<TResult> GetPipeline<TResult>(string key)
    {
        // Only HttpResponseMessage is expected; other types get an empty pipeline.
        if (typeof(TResult) == typeof(HttpResponseMessage))
        {
            var pipeline = _typed.GetOrAdd(key, _ => BuildBreakerPipeline(_opts));
            return (ResiliencePipeline<TResult>)(object)pipeline;
        }

        return ResiliencePipeline<TResult>.Empty;
    }

    public override ResiliencePipeline GetPipeline(string key) => ResiliencePipeline.Empty;

    public override bool TryGetPipeline<TResult>(string key, out ResiliencePipeline<TResult> pipeline)
    {
        pipeline = GetPipeline<TResult>(key);
        return true;
    }

    public override bool TryGetPipeline(string key, out ResiliencePipeline pipeline)
    {
        // Untyped pipelines are unused by the gateway; return Empty so callers
        // that exercise this path get a no-op pipeline. Always succeeds because
        // every key resolves to the shared static Empty instance.
        pipeline = ResiliencePipeline.Empty;
        return true;
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildBreakerPipeline(AgentGatewayOptions opts)
        => new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio       = opts.CircuitBreakerFailureRatio / 100.0,
                MinimumThroughput  = opts.CircuitBreakerMinimumThroughput,
                SamplingDuration   = opts.CircuitBreakerSamplingDuration,
                BreakDuration      = opts.CircuitBreakerBreakDuration,
                ShouldHandle       = static args =>
                    ValueTask.FromResult(ServiceCollectionExtensions.IsTransient(args.Outcome)),
            })
            .Build();
}
