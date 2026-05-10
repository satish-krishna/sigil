# Issue #10 — Agent Gateway (Open tier)

**Status:** Approved — ready for implementation planning
**Date:** 2026-05-09
**Branch:** `feat/agent-gateway`
**Issue:** [#10 — Phase 1 · Secure Gateway (JWT-signed dispatch + Polly)](https://github.com/satish-krishna/sigil/issues/10)
**Blueprint references:** `.bob/docs/sigil-architecture-blueprint.md` §2, §3, §4.5, §4.7, §5
**Depends on:** #3 (protocol types), #4 (Sigil-Key validator + `SigilSecurityOptions`), #19 (registry record shapes)

> The kernel-side HTTP client for calling agents. All dispatch flows through here — no ad-hoc `HttpClient` use anywhere else in the kernel. Open-tier signing only; JWT and mTLS land in Phase 3.

---

## 1. Goal

Land the kernel→agent dispatch surface so the orchestrator (a future issue) can call `/sigil/validate` and `/sigil/execute` against any registered agent without owning HTTP, retry, timeout, or circuit-breaker logic. The gateway is responsible for:

- Resolving outbound credentials per agent (Open tier today; tier dispatch is a fail-fast switch for Standard/Trusted).
- Applying timeout + retry around each call.
- Per-agent circuit-breaker isolation so one sick endpoint doesn't poison healthy ones.
- Mapping HTTP outcomes to `Result<T>` with stable string error codes.
- Emitting one `Activity` per call and one terminal log line per outcome.

The gateway is a leaf component — it knows nothing about plans, jobs, or storage. It takes an `AgentRegistration` and a request DTO and returns a `Result<TResponse>`.

## 2. Scope

### In scope

- `Sigil.Core/Gateway/`: `IAgentGateway` interface, `SigilGatewayErrors` constants. Zero new dependencies.
- `Sigil.Infrastructure/Gateway/`: `AgentGateway` (typed `HttpClient` consumer), `AgentGatewayOptions`, `AddAgentGateway` DI extension.
- `Microsoft.Extensions.Http.Resilience` package added to `Sigil.Infrastructure` via Central Package Management.
- New tests under `tests/Sigil.Infrastructure.Tests/Gateway/` and `tests/Sigil.Core.Tests/Gateway/`.

### Out of scope (deferred)

- **JWT outbound signing** — Phase 3 / later iteration of this issue family. Code path returns `tier-not-supported` for `Standard`.
- **mTLS** — Phase 3. Code path returns `tier-not-supported` for `Trusted`.
- **`IOutboundCredentialProvider` abstraction** — explicitly **not** introduced. Phase 1 has exactly one signing scheme; the abstraction lands when Phase 3 forces a second implementation. Premature interface = YAGNI.
- **The orchestrator that calls the gateway** — separate runtime issue.
- **`POST /api/agents/register` endpoint** — issue #13. The gateway is for kernel→agent traffic; the registration endpoint is agent→kernel.
- **OpenTelemetry exporter SDK wiring** — gateway emits `Activity` via a static `ActivitySource`. The exporter is wired in `Sigil.Api`'s host configuration in a separate observability issue.
- **End-to-end tests against a live agent** — covered by Echo agent (#12) + Docker Compose (#14).

## 3. Design decisions

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| Q1 | Interface name | `IAgentGateway` (no `Secure` prefix) | There will never be an `IInsecureAgentGateway`. The "Secure" prefix is descriptive, not discriminating. |
| Q2 | Method parameter shape | Pass `AgentRegistration` directly | The orchestrator already has the registration in hand. Avoids coupling the gateway to `IAgentRegistrationStore`. `EndpointUrl`, `AgentId`, and `Security.Tier` all live on the record. |
| Q3 | Source of outbound Sigil-Key | Same `SigilSecurityOptions.OpenTier.Keys` allowlist the validator uses | Single source of truth. Symmetric: at Open tier, the agent and kernel share one key per agent. Any divergence (separate inbound/outbound dictionaries) is config sprawl. |
| Q4 | Resilience pipeline shape | Per-agent circuit breaker via `ResiliencePipelineProvider<string>`; timeout + retry on the typed-client handler chain (one named handler per method) | A global circuit breaker would let one sick agent open the breaker for healthy ones — wrong for production. Polly v8's `ResiliencePipelineProvider<TKey>` is built for this. Timeout/retry are stateless and live more naturally on the handler chain. |
| Q5 | Validate vs execute timeouts | Distinct named handlers (`agent-validate`, `agent-execute`) with their own timeouts | Validate is short by design (token budget check, ~5s); execute can run for minutes. A single timeout would have to favor one. |
| Q6 | Behavior for non-Open tiers in Phase 1 | Fail fast with `tier-not-supported` before any HTTP attempt | Matches the validator's behavior; impossible to silently dispatch unauthenticated traffic when JWT/mTLS land later. |
| Q7 | Failure surface | `Result<T>` (CSharpFunctionalExtensions) with kebab-case string error codes | Project convention since #4. Stable, log-safe matching without exception machinery. |
| Q8 | Cancellation handling | Caller's `CancellationToken` flows through; on cancel, return `Result.Failure<T>("cancelled")` rather than letting `OperationCanceledException` propagate | Preserves `Result<T>` discipline at the seam; the orchestrator branches on a code, not on an exception type. |
| Q9 | Retry semantics | Retry transport errors and HTTP 5xx with exponential backoff + jitter, `MaxRetryAttempts = 2` default. **Never** retry 4xx (401/403/404/other). | 4xx outcomes are kernel- or config-side problems — retry won't help and obscures the underlying error. |
| Q10 | Where does the breaker increment | Only on transport errors and 5xx — not on 4xx | A 401 is a configuration mistake, not an unhealthy endpoint. The breaker exists to protect against sick agents. |
| Q11 | Endpoint composition | `agent.EndpointUrl` is treated as a base; `/sigil/validate` and `/sigil/execute` are appended after a trailing-slash-normalisation pass | If `EndpointUrl` includes a path prefix (e.g., `https://host/v1`), naive `Uri(base, "/sigil/...")` would reset to host root. Must compose with explicit trailing-slash handling. |
| Q12 | JSON serialization options | Static `JsonSerializerOptions` on the gateway: `web` defaults, camelCase, the existing core `*JsonConverter`s | Options never vary at runtime. Static field avoids per-call allocation; symmetric on inbound deserialization. |
| Q13 | Observability surface | Static `ActivitySource` (`Sigil.Gateway`) per call + `ILogger` for terminal outcomes | Activity lights up automatically when the API host wires an OTel exporter. Polly v8 emits its own retry telemetry — gateway does not duplicate per-attempt logs. |

## 4. Contracts (new code in `Sigil.Core/Gateway/`)

```csharp
namespace Sigil.Core.Gateway;

public interface IAgentGateway
{
    /// <summary>
    /// Pre-flight validation against an agent: "can you handle this task right now?"
    /// Idempotent. Short timeout. Retried on transient failure.
    /// </summary>
    Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent,
        ValidationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Dispatch a task with its context snapshot to an agent and receive a delta.
    /// Long timeout. Retried on transient failure; not on 4xx.
    /// </summary>
    Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent,
        AgentExecutionPackage package,
        CancellationToken ct = default);
}

public static class SigilGatewayErrors
{
    // Pre-flight (no HTTP attempt)
    public const string TierNotSupported     = "tier-not-supported";
    public const string OutboundKeyMissing   = "outbound-key-missing";
    public const string EndpointInvalid      = "endpoint-invalid";

    // HTTP outcomes
    public const string AgentRejectedCredentials = "agent-rejected-credentials";  // 401/403
    public const string AgentNotFound            = "agent-not-found";             // 404
    public const string AgentRejected            = "agent-rejected";              // other 4xx
    public const string AgentError               = "agent-error";                 // 5xx after retries

    // Transport / resilience
    public const string Timeout         = "timeout";
    public const string CircuitOpen     = "circuit-open";
    public const string TransportError  = "transport-error";

    // Protocol
    public const string ProtocolError   = "protocol-error";   // 2xx body fails to deserialize / required fields missing

    // Cancellation
    public const string Cancelled       = "cancelled";
}
```

The interface is parameterised on the **registration record** (Q2) so callers don't have to redundantly pluck out `AgentId` / `EndpointUrl` / `Tier` and the gateway has a single object to log/trace from.

## 5. Implementation (`Sigil.Infrastructure/Gateway/`)

### 5.1 Options

```csharp
namespace Sigil.Infrastructure.Gateway;

public sealed class AgentGatewayOptions
{
    public const string SectionName = "Gateway";

    public TimeSpan ValidateTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ExecuteTimeout  { get; set; } = TimeSpan.FromSeconds(120);

    public int      MaxRetryAttempts { get; set; } = 2;
    public TimeSpan BaseRetryDelay   { get; set; } = TimeSpan.FromMilliseconds(200);

    public int      CircuitBreakerFailureRatio       { get; set; } = 50;     // percent
    public int      CircuitBreakerMinimumThroughput  { get; set; } = 10;     // calls in window
    public TimeSpan CircuitBreakerSamplingDuration   { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration      { get; set; } = TimeSpan.FromSeconds(15);
}
```

Bound from configuration section `Gateway`. Validated on start. Field-style mutability matches the `SigilSecurityOptions` precedent.

### 5.2 DI extension

```csharp
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

        services.AddHttpClient<AgentGateway>()
            .AddResilienceHandler("agent-validate", BuildValidatePipeline)
            .AddResilienceHandler("agent-execute",  BuildExecutePipeline);

        // Single registry registered as a singleton ResiliencePipelineProvider<string>.
        // The registry is configured to lazily build a per-agent breaker pipeline
        // for any key matching "agent-circuit::*" on first lookup.
        services.AddResiliencePipelineRegistry<string>(ConfigureBreakerRegistry);

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
            .AddRetry(BuildRetryStrategy(opts));
    }

    private static void BuildExecutePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext ctx)
    {
        var opts = ctx.ServiceProvider.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
        builder
            .AddTimeout(opts.ExecuteTimeout)
            .AddRetry(BuildRetryStrategy(opts));
    }

    private static HttpRetryStrategyOptions BuildRetryStrategy(AgentGatewayOptions opts) => new()
    {
        MaxRetryAttempts = opts.MaxRetryAttempts,
        Delay            = opts.BaseRetryDelay,
        BackoffType      = DelayBackoffType.Exponential,
        UseJitter        = true,
        ShouldHandle     = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
    };

    // Shared transient predicate: retry/breaker fire on transport exceptions
    // and HTTP 5xx. Never on 4xx (those are mapped terminally in §5.5).
    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
            return true;
        if (outcome.Result is { } response)
            return (int)response.StatusCode >= 500;
        return false;
    }

    // Per-agent circuit-breaker pipelines built lazily on first lookup of any
    // "agent-circuit::*" key. Pipeline configuration is identical per-agent;
    // only the per-key state (open/closed/half-open) is isolated.
    private static void ConfigureBreakerRegistry(
        ResiliencePipelineRegistryOptions<string> options,
        IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
        options.BuilderFactory = key => new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio       = opts.CircuitBreakerFailureRatio / 100.0,
                MinimumThroughput  = opts.CircuitBreakerMinimumThroughput,
                SamplingDuration   = opts.CircuitBreakerSamplingDuration,
                BreakDuration      = opts.CircuitBreakerBreakDuration,
                ShouldHandle       = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
            });
    }
}
```

> The `ConfigureBreakerRegistry` shape above is the design intent. The exact `AddResiliencePipelineRegistry<TKey>` overload signature in `Polly.Extensions` v8.x — specifically whether the configuration callback exposes `IServiceProvider` directly or requires an intermediate `IConfigureOptions<ResiliencePipelineRegistryOptions<TKey>>` — will be confirmed at implementation time. If the API forces the latter, the registration is split accordingly without changing the design.

### 5.3 The gateway itself

```csharp
public sealed class AgentGateway : IAgentGateway
{
    public static readonly ActivitySource ActivitySource = new("Sigil.Gateway", "1.0.0");

    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<SigilSecurityOptions> _securityOptions;
    private readonly ResiliencePipelineProvider<string> _breakers;
    private readonly ILogger<AgentGateway> _logger;

    public AgentGateway(
        HttpClient http,
        IOptionsMonitor<SigilSecurityOptions> securityOptions,
        ResiliencePipelineProvider<string> breakers,
        ILogger<AgentGateway> logger)
    {
        _http            = http;
        _securityOptions = securityOptions;
        _breakers        = breakers;
        _logger          = logger;
    }

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default)
        => DispatchAsync<ValidationRequest, ValidationResult>(
            agent, request, path: "/sigil/validate", method: "validate", ct);

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default)
        => DispatchAsync<AgentExecutionPackage, AgentExecutionResult>(
            agent, package, path: "/sigil/execute", method: "execute", ct);

    // Generic body — runs the same pre-flight checks, builds the request,
    // resolves the per-agent breaker, calls _http.SendAsync inside the breaker,
    // maps response → Result. Method name (validate/execute) determines which
    // named resilience handler the typed HttpClient already wraps around the call.
    private async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(
        AgentRegistration agent, TRequest body, string path, string method,
        CancellationToken ct) { /* see §5.4 */ }
}
```

### 5.4 Dispatch flow

```mermaid
sequenceDiagram
    participant Caller
    participant GW as AgentGateway
    participant Sec as SigilSecurityOptions
    participant Breakers as ResiliencePipelineProvider
    participant HC as HttpClient (named handler)
    participant Agent

    Caller->>GW: ExecuteAsync(agent, package, ct)
    GW->>GW: Start Activity "agent.execute"
    GW->>GW: Validate agent.EndpointUrl (absolute URI)
    GW->>GW: Tier == Open? else fail tier-not-supported
    GW->>Sec: OpenTier.Keys[agent.AgentId.Value]
    Sec-->>GW: outbound key (or absent → outbound-key-missing)
    GW->>GW: Compose request: POST {endpoint}/sigil/execute<br/>Headers: X-Sigil-Key, Content-Type: application/json<br/>Body: JsonSerializer.Serialize(package, JsonOptions)
    GW->>Breakers: GetPipeline("agent-circuit::{agentId}")
    Breakers-->>GW: ResiliencePipeline<HttpResponseMessage>
    GW->>HC: breaker.Execute(_http.SendAsync(request, ct))
    Note over HC: Named handler (agent-execute) wraps SendAsync<br/>with timeout + retry
    HC->>Agent: POST /sigil/execute
    Agent-->>HC: 200 OK + JSON
    HC-->>GW: HttpResponseMessage
    GW->>GW: Map status → success | error code
    GW->>GW: Deserialize body → AgentExecutionResult
    GW->>GW: Activity.Status = Ok; log terminal
    GW-->>Caller: Result.Success(AgentExecutionResult)
```

`ValidateAsync` is identical except path `/sigil/validate`, body `ValidationRequest`, response `ValidationResult`, named handler `agent-validate` (short timeout). The breaker pipeline is **shared** across both methods for a given agent — a sick agent should trip the breaker regardless of which endpoint surfaced the failure first.

### 5.5 HTTP outcome mapping

| HTTP / signal | Action | Result |
|---|---|---|
| 2xx, body deserializes | Activity.Ok, debug log | `Result.Success(T)` |
| 2xx, body fails to deserialize | Activity.Error, warn log | `Result.Failure(ProtocolError)` |
| 401, 403 | Activity.Error, warn log; **no retry**, **no breaker increment** | `Result.Failure(AgentRejectedCredentials)` |
| 404 | Activity.Error, warn log; no retry, no breaker increment | `Result.Failure(AgentNotFound)` |
| Other 4xx | Activity.Error, warn log; no retry, no breaker increment | `Result.Failure(AgentRejected)` |
| 5xx after retries | Activity.Error, error log; breaker incremented | `Result.Failure(AgentError)` |
| Polly timeout | Activity.Error, warn log; breaker incremented | `Result.Failure(Timeout)` |
| Breaker open | Activity.Error, debug log (don't spam — Polly already logs the open transition) | `Result.Failure(CircuitOpen)` |
| Connection refused / DNS / TLS / `HttpRequestException` after retries | Activity.Error, error log; breaker incremented | `Result.Failure(TransportError)` |
| Caller cancels (`ct` fires) | Activity.Error, debug log; **no breaker increment** | `Result.Failure(Cancelled)` |
| Pre-flight tier mismatch | Activity not started | `Result.Failure(TierNotSupported)` |
| Pre-flight no key in allowlist | Activity not started | `Result.Failure(OutboundKeyMissing)` |
| Pre-flight invalid endpoint URL | Activity not started | `Result.Failure(EndpointInvalid)` |

### 5.6 Endpoint composition

```csharp
private static Uri ComposeEndpoint(string endpointUrl, string subPath)
{
    if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var baseUri))
        throw new InvalidOperationException(/* surfaced as endpoint-invalid */);
    var basePath = baseUri.AbsoluteUri.TrimEnd('/');
    return new Uri(basePath + subPath, UriKind.Absolute);
}
```

`subPath` is always one of `"/sigil/validate"` or `"/sigil/execute"` (compile-time constants on the gateway). This preserves any path prefix on `endpointUrl` (e.g., `https://host/v1` + `/sigil/execute` → `https://host/v1/sigil/execute`).

### 5.7 JSON serialization

```csharp
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
```

Static. Built once. Reused for both serialization (request body) and deserialization (response body). Symmetric with what the SDK (#8) will use on the agent side.

## 6. Observability

### 6.1 Activity

```csharp
public static readonly ActivitySource ActivitySource = new("Sigil.Gateway", "1.0.0");
```

Each `DispatchAsync` call starts an activity with name `agent.validate` or `agent.execute`. Tags set at start:

| Tag | Value |
|---|---|
| `sigil.agent.id` | `agent.AgentId.Value` |
| `sigil.agent.endpoint` | `agent.EndpointUrl` |
| `sigil.agent.tier` | `agent.Security.Tier` (string) |
| `sigil.gateway.method` | `validate` or `execute` |
| `sigil.job.id` | `request.Task.JobId.Value` (validate) or `package.Task.JobId.Value` (execute) |
| `sigil.step.id` | `request.Task.StepId.Value` or `package.Task.StepId.Value` |

On failure:

| Tag | Value |
|---|---|
| `sigil.gateway.error_code` | The string code returned in `Result.Failure` |
| `http.response.status_code` | The HTTP status, when one was received |
| `sigil.gateway.attempts` | Best-effort: total attempts made, read from `ResilienceContext` if available |

`Activity.Status` is set to `Error` for any failed `Result`.

### 6.2 Logging

`ILogger<AgentGateway>` emits one log line per terminal outcome with structured properties matching the activity tags. Levels:

- `LogDebug` for success and `Cancelled` / `CircuitOpen` (these are routine).
- `LogWarning` for `AgentRejectedCredentials`, `AgentNotFound`, `AgentRejected`, `Timeout`, `ProtocolError`, `TierNotSupported`, `OutboundKeyMissing`, `EndpointInvalid`.
- `LogError` for `AgentError`, `TransportError`.

No per-attempt logging — Polly v8 emits its own retry telemetry via `ResilienceTelemetry`; the host's logger provider picks it up automatically.

## 7. Tests

### 7.1 `tests/Sigil.Infrastructure.Tests/Gateway/`

A `FakeHttpMessageHandler` records each request and lets each test queue scripted responses. Two harness builders:

```csharp
internal static class GatewayTestHarness
{
    // Bypass-resilience: AgentGateway with raw HttpClient(handler), no Polly.
    // Used for tests that assert request shape, headers, body, response mapping.
    public static AgentGateway WithRawClient(
        FakeHttpMessageHandler handler,
        SigilSecurityOptions security = null,
        AgentGatewayOptions gateway = null);

    // Resilience-on: full ServiceCollection + AddAgentGateway with the FakeHandler
    // injected via .ConfigurePrimaryHttpMessageHandler. Used for retry/timeout/breaker tests.
    public static IAgentGateway WithResilience(
        FakeHttpMessageHandler handler,
        SigilSecurityOptions security = null,
        AgentGatewayOptions gateway = null);
}
```

Scenario coverage:

| File | Scenarios |
|---|---|
| `AgentGatewayValidateTests` | Happy path returns `Result.Success(ValidationResult)`. Path is `{endpoint}/sigil/validate`. `X-Sigil-Key` header present and equals allowlist value. Request body deserializes to `ValidationRequest`. Response body deserialized correctly. 2xx with malformed JSON → `protocol-error`. |
| `AgentGatewayExecuteTests` | Happy path returns `Result.Success(AgentExecutionResult)`. Path is `{endpoint}/sigil/execute`. Request body matches expected JSON shape. Endpoint with trailing slash composes correctly. Endpoint with path prefix preserves the prefix. Endpoint missing scheme → `endpoint-invalid`. |
| `AgentGatewayTierTests` | `Standard` tier → `tier-not-supported`, no HTTP call made (handler records zero requests). `Trusted` tier → `tier-not-supported`. `Open` tier with no allowlist entry for `agentId` → `outbound-key-missing`. Empty / null `EndpointUrl` → `endpoint-invalid`. |
| `AgentGatewayResilienceTests` | 5xx retried `MaxRetryAttempts` times then `agent-error`. 401 → `agent-rejected-credentials` (zero retries — handler records exactly one request). 403 → `agent-rejected-credentials`. 404 → `agent-not-found`. Other 4xx → `agent-rejected`. `HttpRequestException` retried then `transport-error`. Polly timeout → `timeout`. `CancellationToken` fired mid-flight → `cancelled` (no breaker increment — verified by following call against the same agent succeeding). Per-agent breaker opens after threshold and returns `circuit-open` until break-duration elapses. Separate agent's breaker stays closed (proves per-agent isolation). Validate timeout shorter than execute timeout (verified by configuring tiny validate timeout, larger execute timeout, and observing which one fires). |

### 7.2 `tests/Sigil.Core.Tests/Gateway/`

`SigilGatewayErrorsTests` — constants are non-empty, kebab-case, and stable. Mirrors `SigilSecurityErrorsTests` from #4.

### 7.3 What's not tested in this issue

- End-to-end against a real agent — Echo + Docker Compose (#12 + #14).
- Gateway integration with a populated `IAgentRegistrationStore` — separate orchestrator issue.
- Polly's own retry / breaker mechanics — those are covered by the upstream library's tests; we test our wiring of them.

## 8. Configuration sample

```json
// Sigil.Api/appsettings.json
{
  "Security": {
    "Mode": "Open",
    "OpenTier": {
      "Keys": {
        "echo-agent": "dev-key-echo"
      }
    }
  },
  "Gateway": {
    "ValidateTimeout": "00:00:05",
    "ExecuteTimeout":  "00:02:00",
    "MaxRetryAttempts": 2,
    "BaseRetryDelay":   "00:00:00.200",
    "CircuitBreakerFailureRatio":      50,
    "CircuitBreakerMinimumThroughput": 10,
    "CircuitBreakerSamplingDuration":  "00:00:30",
    "CircuitBreakerBreakDuration":     "00:00:15"
  }
}
```

## 9. Dependencies and package additions

- `Microsoft.Extensions.Http.Resilience` — added to `Sigil.Infrastructure.csproj` via Central Package Management. Pulls in `Polly`, `Polly.Extensions`, `Microsoft.Extensions.Resilience` transitively.
- `System.Diagnostics.DiagnosticSource` — already transitive; no new direct reference needed.
- No package additions in `Sigil.Core` — the contract layer stays at `CSharpFunctionalExtensions` only.

The package's minimum version will be selected at implementation time to satisfy the existing `Microsoft.Extensions.*` 10.0.5 baseline already in CPM.

## 10. Open questions / known unknowns

These are flagged for the implementation phase to resolve, not blockers for the spec:

1. **`AddResiliencePipelineRegistry<TKey>` configuration callback shape.** The design pattern (lazy-create a breaker on first lookup of `agent-circuit::*` via a `BuilderFactory`) is correct. The literal overload signature in the installed `Polly.Extensions` v8.x — and whether `IServiceProvider` is plumbed directly into the callback or accessed via an `IConfigureOptions<ResiliencePipelineRegistryOptions<TKey>>` indirection — will be confirmed at implementation time and the DI snippet in §5.2 updated to match.
2. **`ResilienceContext` attempt count exposure.** The plan is to pull `attempts` for the failure log/tag. If the v8 API doesn't expose this cleanly, the tag is dropped — it's a nice-to-have, not load-bearing.
3. **Breaker key collisions.** Using `agent-circuit::{agentId}` assumes `AgentId.Value` is safe as a registry-key suffix. `AgentId` is currently a `readonly record struct(string Value)` with no normalisation; if registry keys turn out to be case-sensitive in a way that surprises us with mixed-case ids, we add a normalisation pass in `BuildBreakerKey`.
4. **Should the breaker also wrap pre-flight failures?** Decision: **no** — pre-flight (tier, key, URL) outcomes are kernel-side, not agent-health signals. The breaker only sees outcomes of the actual HTTP send.

## 11. Acceptance checklist

From issue #10, mapped to deliverables in this spec:

- [ ] `SecureAgentGateway` in `Sigil.Infrastructure/Gateway` — **renamed to `AgentGateway`** (Q1). §5.3.
- [ ] Signs outbound requests (Open tier: Sigil-Key header; JWT wired for later). §5.4, §5.5; JWT explicitly deferred to Phase 3 (§2 Out of scope).
- [ ] Polly pipeline: timeout + retry + circuit breaker. §5.2 (timeout/retry on named handlers), §5.2 + §5.4 (per-agent breaker).
- [ ] `ValidateAsync(agent, task)` + `ExecuteAsync(agent, package)` methods. §4.
- [ ] Unit tests with a fake `HttpMessageHandler`. §7.1.
