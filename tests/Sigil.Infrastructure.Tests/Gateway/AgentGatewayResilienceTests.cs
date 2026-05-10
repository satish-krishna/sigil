using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Security;
using Sigil.Infrastructure.Gateway;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayResilienceTests
{
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";
    private const string Key = "dev-key-echo";

    // One resilience handler (agent-retry) is registered on the typed HttpClient — retry
    // only, no timeout. Per-method timeouts are applied by the gateway via a linked
    // CancellationTokenSource. A single transient dispatch makes 1 + MaxRetryAttempts=2
    // HTTP calls total. The per-agent circuit breaker wraps _http.SendAsync (the full
    // typed-client pipeline), so it observes ONE outcome per breaker.ExecuteAsync call.
    private const int AttemptsPerDispatch = 3; // 1 + MaxRetryAttempts=2

    private static SigilSecurityOptions OpenWith(params (string AgentId, string Key)[] entries)
    {
        var opts = new SigilSecurityOptions { Mode = SecurityTier.Open };
        foreach (var (id, k) in entries) opts.OpenTier.Keys[id] = k;
        return opts;
    }

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
        AvailableTokenBudget = 1000
    };

    private static AgentGatewayOptions FastBreakerOptions() => new()
    {
        ValidateTimeout = TimeSpan.FromSeconds(5),
        ExecuteTimeout  = TimeSpan.FromSeconds(5),
        MaxRetryAttempts = 2,
        BaseRetryDelay = TimeSpan.FromMilliseconds(10),
        CircuitBreakerFailureRatio = 50,
        CircuitBreakerMinimumThroughput = 4,
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(2),
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(60),
    };

    [Fact]
    public async Task FiveXX_Is_Retried_Then_Surfaces_AgentError()
    {
        var handler = new FakeHttpMessageHandler();
        // One retry handler with MaxRetryAttempts=2: 1 initial + 2 retries = 3 HTTP calls total.
        for (int i = 0; i < AttemptsPerDispatch; i++)
            handler.EnqueueResponse(HttpStatusCode.InternalServerError);

        var (gateway, provider) = GatewayTestHarness.WithResilience(
            handler,
            security: OpenWith((AgentIdValue, Key)),
            gateway: FastBreakerOptions());
        using var _ = provider;

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.AgentError);
        handler.Requests.Count.ShouldBe(AttemptsPerDispatch);
    }

    [Fact]
    public async Task FourXX_Is_Not_Retried()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var (gateway, provider) = GatewayTestHarness.WithResilience(
            handler,
            security: OpenWith((AgentIdValue, Key)),
            gateway: FastBreakerOptions());
        using var _ = provider;

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.AgentRejectedCredentials);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PerAgent_Breaker_Isolates_Sick_Agent_From_Healthy_Agent()
    {
        var handler = new FakeHttpMessageHandler();

        // The per-agent breaker wraps _http.SendAsync (the full typed-client pipeline including
        // retry handlers). The breaker sees ONE outcome per breaker.ExecuteAsync call.
        // CircuitBreakerMinimumThroughput=4, FailureRatio=50% — the breaker opens after
        // the 4th consecutive 5xx outcome. Each dispatch to agent-a produces AttemptsPerDispatch
        // (3) internal HTTP calls but counts as ONE breaker outcome (a failure).
        // We make 4 calls to agent-a to satisfy MinimumThroughput, then the next call
        // should be rejected with circuit-open. Queue exactly 4 × 3 = 12 5xx responses
        // so agent-b's OK responses sit immediately next in the shared handler queue.
        const int SickCallsToTripBreaker = 4;
        for (int i = 0; i < SickCallsToTripBreaker * AttemptsPerDispatch; i++)
            handler.EnqueueResponse(HttpStatusCode.InternalServerError);

        // Healthy responses for agent B — positioned directly after agent-a's 5xx pool.
        var ok = JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() });
        for (int i = 0; i < 2; i++)
            handler.EnqueueResponse(HttpStatusCode.OK, ok);

        var (gateway, provider) = GatewayTestHarness.WithResilience(
            handler,
            security: OpenWith(("agent-a", "k-a"), ("agent-b", "k-b")),
            gateway: FastBreakerOptions());
        using var _ = provider;

        var agentA = GatewayTestHarness.MakeRegistration("agent-a", EndpointUrl);
        var agentB = GatewayTestHarness.MakeRegistration("agent-b", EndpointUrl);

        // Drive agent-a's breaker open (4 failed outcomes = MinimumThroughput met, 100% > 50% ratio)
        for (int i = 0; i < SickCallsToTripBreaker; i++)
            await gateway.ValidateAsync(agentA, SampleRequest());

        // Pin the actual attempt count so a future Polly behavior change produces
        // a clean Shouldly failure rather than an opaque "queue an EnqueueResponse"
        // exception when the shared response queue is consumed unexpectedly.
        handler.Requests.Count.ShouldBe(SickCallsToTripBreaker * AttemptsPerDispatch);

        // Next call to agent-a should fail fast with circuit-open
        var aFinal = await gateway.ValidateAsync(agentA, SampleRequest());
        aFinal.IsFailure.ShouldBeTrue();
        aFinal.Error.ShouldBe(SigilGatewayErrors.CircuitOpen);

        // Agent-b's breaker is independent — it should still succeed.
        var bResult = await gateway.ValidateAsync(agentB, SampleRequest());
        bResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Timeout_Surfaces_TimeoutErrorCode()
    {
        var handler = new FakeHttpMessageHandler();
        // Configure a delay longer than the per-method timeout. The gateway's
        // CancellationTokenSource.CancelAfter(ValidateTimeout) will fire before
        // the synchronous Thread.Sleep completes, causing the linked CTS to
        // cancel SendAsync.
        // NOTE: Thread.Sleep in FakeHttpMessageHandler blocks the worker thread for
        // the full 500ms regardless of cancellation — wall-clock time for this test
        // is ~500ms. The gateway still returns Timeout because the linked CTS fires
        // at 50ms and the awaited SendAsync task throws OperationCanceledException.
        handler.EnqueueDelay(TimeSpan.FromMilliseconds(500));

        var fastTimeoutOpts = new AgentGatewayOptions
        {
            ValidateTimeout = TimeSpan.FromMilliseconds(50),
            ExecuteTimeout = TimeSpan.FromSeconds(5),
            MaxRetryAttempts = 1,  // minimum valid; timeout fires long before any retry fires
            BaseRetryDelay = TimeSpan.FromMilliseconds(1),
            CircuitBreakerFailureRatio = 50,
            CircuitBreakerMinimumThroughput = 10,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(2),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(60),
        };

        var (gateway, provider) = GatewayTestHarness.WithResilience(
            handler,
            security: OpenWith((AgentIdValue, Key)),
            gateway: fastTimeoutOpts);
        using var _ = provider;

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.Timeout);
    }

    [Fact]
    public async Task HttpRequestException_Is_Retried_Then_Surfaces_TransportError()
    {
        var handler = new FakeHttpMessageHandler();
        // 1 + MaxRetryAttempts(2) = 3 total attempts before surrendering.
        for (int i = 0; i < AttemptsPerDispatch; i++)
            handler.EnqueueException(new HttpRequestException("connection refused"));

        var (gateway, provider) = GatewayTestHarness.WithResilience(
            handler,
            security: OpenWith((AgentIdValue, Key)),
            gateway: FastBreakerOptions());
        using var _ = provider;

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TransportError);
        handler.Requests.Count.ShouldBe(AttemptsPerDispatch);
    }
}
