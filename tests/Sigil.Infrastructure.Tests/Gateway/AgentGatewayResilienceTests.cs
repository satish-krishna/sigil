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

    // NOTE: AddAgentGateway registers two named resilience handlers — agent-validate and
    // agent-execute — on the same typed HttpClient. They chain in sequence, so a single
    // gateway call makes (1 + MaxRetryAttempts)^2 HTTP attempts when all responses are
    // transient. With MaxRetryAttempts=2: (1+2)*(1+2) = 9 actual HTTP calls per dispatch.
    // The per-agent circuit breaker wraps _http.SendAsync (the full typed-client pipeline),
    // so it observes ONE outcome per breaker.ExecuteAsync call regardless of inner retries.
    private const int AttemptsPerDispatch = 9; // (1+2)*(1+2) for MaxRetryAttempts=2

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
        // Two stacked resilience handlers (agent-validate + agent-execute), each with
        // MaxRetryAttempts=2, multiply: (1+2) * (1+2) = 9 HTTP calls total.
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
        // the 4th consecutive 5xx outcome. Each dispatch to agent-a consumes AttemptsPerDispatch
        // internal HTTP calls but counts as ONE breaker outcome (a failure).
        // We make 4 calls to agent-a to satisfy MinimumThroughput, then the next call
        // should be rejected with circuit-open.
        const int SickCallsToTripBreaker = 4;
        for (int i = 0; i < SickCallsToTripBreaker * AttemptsPerDispatch; i++)
            handler.EnqueueResponse(HttpStatusCode.InternalServerError);

        // Healthy responses for agent B
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

        // Next call to agent-a should fail fast with circuit-open
        var aFinal = await gateway.ValidateAsync(agentA, SampleRequest());
        aFinal.IsFailure.ShouldBeTrue();
        aFinal.Error.ShouldBe(SigilGatewayErrors.CircuitOpen);

        // Agent-b's breaker is independent — it should still succeed.
        var bResult = await gateway.ValidateAsync(agentB, SampleRequest());
        bResult.IsSuccess.ShouldBeTrue();
    }
}
