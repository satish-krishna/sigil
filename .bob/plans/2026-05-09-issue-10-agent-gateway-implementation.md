# Issue #10 — Agent Gateway Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the kernel→agent dispatch surface (`IAgentGateway` in `Sigil.Core`, `AgentGateway` in `Sigil.Infrastructure`) so the future orchestrator can call `/sigil/validate` and `/sigil/execute` on registered agents with timeout, retry, and per-agent circuit-breaker isolation. Open-tier signing only; Standard/Trusted fail fast with `tier-not-supported`.

**Architecture:** `IAgentGateway` interface + `SigilGatewayErrors` string constants in `Sigil.Core`. Concrete `AgentGateway` is a typed-`HttpClient` consumer in `Sigil.Infrastructure`. Per-method resilience pipelines (`agent-validate`, `agent-execute`) live on the typed-client handler chain (timeout + retry). Per-agent circuit breaker is a separate `ResiliencePipeline<HttpResponseMessage>` resolved from `ResiliencePipelineProvider<string>` and wrapped around each `SendAsync` call inside the gateway. Outbound `X-Sigil-Key` is sourced from the same `SigilSecurityOptions.OpenTier.Keys` allowlist the validator uses. JSON via a static `JsonSerializerOptions` reusing the core `*JsonConverter` types. `Result<T>` discipline at every seam — exceptions are mapped to error codes, never propagated.

**Tech Stack:** .NET 9, FastEndpoints v8, `CSharpFunctionalExtensions.Result`, `Microsoft.Extensions.Http.Resilience` (Polly v8 under the hood), `Microsoft.Extensions.*` 10.0.5 line, xUnit + Shouldly. Central package management via `Directory.Packages.props`.

**Spec:** [`.bob/plans/2026-05-09-issue-10-agent-gateway.md`](./2026-05-09-issue-10-agent-gateway.md)

---

## Pre-flight notes (read before starting)

- **Working directory:** `D:\Repos\sigil`. All paths are relative to repo root.
- **Branch:** `feat/agent-gateway` (already created; spec already committed there).
- **Build/test commands:** `dotnet build sigil.sln` and `dotnet test sigil.sln`. `TreatWarningsAsErrors=true` is on globally — nullability warnings, unused usings, and analyzer warnings will fail the build.
- **Test framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`) with Shouldly. **Do not** use FluentAssertions — Sigil standardizes on Shouldly.
- **`Result<T>` type:** `CSharpFunctionalExtensions.Result<T>`. Construct via `Result.Success(value)` / `Result.Failure<T>("error-code")`. Inspect via `result.IsSuccess`, `result.IsFailure`, `result.Value`, `result.Error`.
- **`AgentRegistration`:** `Sigil.Core.Registry.AgentRegistration` is a sealed record with `AgentId AgentId`, `string EndpointUrl`, `SecurityProfile Security`, and `ModelSpec Model` (required). Test factories should set the `Model` property to a non-null `ModelSpec` to satisfy the `required` constraint.
- **`SecurityTier`:** `Open | Standard | Trusted`. Defaults to `Open` on `SecurityProfile.Tier`.
- **Existing `TestOptionsMonitor<T>`:** `tests/Sigil.Infrastructure.Tests/Security/TestOptionsMonitor.cs` already exists from #4 — **reuse it** for `AgentGatewayOptions` and `SigilSecurityOptions` plumbing. Do not duplicate.
- **`Microsoft.Extensions.Http.Resilience` version selection (Task 1):** the existing CPM has `Microsoft.Extensions.*` pinned at `10.0.5`. Pick the `Microsoft.Extensions.Http.Resilience` version that aligns with the 10.0 .NET line. If `restore` warns about transitive version conflicts (NU1605/NU1109) under `CentralPackageTransitivePinningEnabled=true`, raise the conflicting transitive packages in CPM to the version `Resilience` requires rather than downgrading `Resilience`. Document the chosen version in the Task 1 commit message.
- **`AddResiliencePipelineRegistry<TKey>` callback shape:** §10 of the spec flags this as an implementation-time API resolution. The plan's Task 12 uses the design-intent shape from the spec; if the installed `Polly.Extensions` overload exposes the configuration via `IConfigureOptions<ResiliencePipelineRegistryOptions<string>>` instead of an inline `Action<ResiliencePipelineRegistryOptions<TKey>, IServiceProvider>`, adapt the wiring (and update the spec's §5.2 snippet in a follow-up commit on the same PR).
- **Hooks:** `PostToolUse` runs Prettier on edited `.cs` files (per `.claude/settings.json`). If a Prettier reformat changes whitespace after a Write/Edit, accept it and proceed.
- **Commit cadence:** one commit per task. Conventional-commit prefixes: `feat(core)`, `feat(infra)`, `chore(build)`, `test(core)`, `test(infra)`. Match recent log style: `feat(infra): Sigil-Key validation, Open tier (closes #4) (#26)`.

### File structure summary

| File | Responsibility | Created/modified in |
|---|---|---|
| `Directory.Packages.props` | Add `Microsoft.Extensions.Http.Resilience` (and any transitive raises) | Task 1 |
| `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj` | Reference the new package | Task 2 |
| `src/Sigil.Core/Gateway/IAgentGateway.cs` | Public dispatch interface | Task 3 |
| `src/Sigil.Core/Gateway/SigilGatewayErrors.cs` | Stable string error-code constants | Task 4 |
| `src/Sigil.Infrastructure/Gateway/AgentGatewayOptions.cs` | `IOptions`-bound config | Task 5 |
| `tests/Sigil.Infrastructure.Tests/Gateway/FakeHttpMessageHandler.cs` | Recording fake for HTTP traffic | Task 6 |
| `tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs` | Builds an `AgentGateway` for tests (raw or DI) | Task 6 |
| `src/Sigil.Infrastructure/Gateway/AgentGateway.cs` | The typed `HttpClient` consumer; grows across Tasks 7–13 | Tasks 7–13 |
| `src/Sigil.Infrastructure/Gateway/ServiceCollectionExtensions.cs` | `AddAgentGateway` DI extension | Task 12 |

Test files mirror this layout under `tests/Sigil.Infrastructure.Tests/Gateway/` and are introduced alongside the code that exercises them.

---

## Task 1: Add `Microsoft.Extensions.Http.Resilience` to CPM

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Identify the right version**

The existing CPM pins `Microsoft.Extensions.*` at `10.0.5`. `Microsoft.Extensions.Http.Resilience` ships in lockstep with the 10.0 .NET runtime line. If `dotnet list package` or NuGet search shows `9.x.x` as the latest, that's because the package family lags the runtime — pick the **highest stable `Microsoft.Extensions.Http.Resilience` version that does not force any of the already-pinned `Microsoft.Extensions.*` packages above `10.0.5`**.

If a stable 10.x is available, prefer it. If only 9.x is available and `restore` succeeds, use that — `Microsoft.Extensions.Http.Resilience 9.x` is API-compatible with `Microsoft.Extensions.* 10.x` (the runtime is upward-compatible by policy).

- [ ] **Step 2: Add the package version**

Edit `Directory.Packages.props`. Add a new `<ItemGroup>` (or append to an existing one) for resilience-related packages:

```xml
  <!-- Resilience -->
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="<chosen-version>" />
  </ItemGroup>
```

Replace `<chosen-version>` with the version selected in Step 1.

- [ ] **Step 3: Verify the solution still restores**

```bash
dotnet restore sigil.sln
dotnet build sigil.sln
```

Expected: clean restore + build, no warnings. No projects reference the new package yet, so this is a no-op for the build graph; the goal is to confirm CPM doesn't trip a transitive-version conflict (NU1605/NU1109).

If a conflict surfaces: read the error to find which `Microsoft.Extensions.*` package the resilience family wants pulled higher, raise that pin in CPM, and re-run.

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore(build): pin Microsoft.Extensions.Http.Resilience"
```

---

## Task 2: Reference the resilience package from `Sigil.Infrastructure`

**Files:**
- Modify: `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj`

- [ ] **Step 1: Add the package reference**

Replace `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="CSharpFunctionalExtensions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Sigil.Core\Sigil.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify the project builds**

```bash
dotnet build src/Sigil.Infrastructure/Sigil.Infrastructure.csproj
```

Expected: clean build. The package is wired in but no source code uses it yet.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Infrastructure/Sigil.Infrastructure.csproj
git commit -m "chore(build): reference Microsoft.Extensions.Http.Resilience in Sigil.Infrastructure"
```

---

## Task 3: `IAgentGateway` interface

**Files:**
- Create: `src/Sigil.Core/Gateway/IAgentGateway.cs`

The interface itself has no behavior to test; it gets exercised through the validator in later tasks. We commit it standalone so subsequent tasks have a stable compile target.

- [ ] **Step 1: Create the interface**

Create `src/Sigil.Core/Gateway/IAgentGateway.cs`:

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

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
```

- [ ] **Step 2: Verify the solution still builds**

```bash
dotnet build sigil.sln
```

Expected: clean build.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Gateway/IAgentGateway.cs
git commit -m "feat(core): add IAgentGateway dispatch interface"
```

---

## Task 4: `SigilGatewayErrors` constants + stability test

**Files:**
- Create: `src/Sigil.Core/Gateway/SigilGatewayErrors.cs`
- Create: `tests/Sigil.Core.Tests/Gateway/SigilGatewayErrorsTests.cs`

Mirrors the `SigilSecurityErrors` pattern from #4: pin the strings with a unit test so a typo at the boundary fails loudly.

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Core.Tests/Gateway/SigilGatewayErrorsTests.cs`:

```csharp
using Shouldly;
using Sigil.Core.Gateway;
using Xunit;

namespace Sigil.Core.Tests.Gateway;

public class SigilGatewayErrorsTests
{
    [Fact]
    public void ErrorCodes_HaveStableValues()
    {
        // Pre-flight
        SigilGatewayErrors.TierNotSupported.ShouldBe("tier-not-supported");
        SigilGatewayErrors.OutboundKeyMissing.ShouldBe("outbound-key-missing");
        SigilGatewayErrors.EndpointInvalid.ShouldBe("endpoint-invalid");

        // HTTP outcomes
        SigilGatewayErrors.AgentRejectedCredentials.ShouldBe("agent-rejected-credentials");
        SigilGatewayErrors.AgentNotFound.ShouldBe("agent-not-found");
        SigilGatewayErrors.AgentRejected.ShouldBe("agent-rejected");
        SigilGatewayErrors.AgentError.ShouldBe("agent-error");

        // Transport / resilience
        SigilGatewayErrors.Timeout.ShouldBe("timeout");
        SigilGatewayErrors.CircuitOpen.ShouldBe("circuit-open");
        SigilGatewayErrors.TransportError.ShouldBe("transport-error");

        // Protocol / cancellation
        SigilGatewayErrors.ProtocolError.ShouldBe("protocol-error");
        SigilGatewayErrors.Cancelled.ShouldBe("cancelled");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilGatewayErrorsTests"
```

Expected: build error — `SigilGatewayErrors` does not exist.

- [ ] **Step 3: Create the constants**

Create `src/Sigil.Core/Gateway/SigilGatewayErrors.cs`:

```csharp
namespace Sigil.Core.Gateway;

public static class SigilGatewayErrors
{
    // Pre-flight (no HTTP attempt)
    public const string TierNotSupported   = "tier-not-supported";
    public const string OutboundKeyMissing = "outbound-key-missing";
    public const string EndpointInvalid    = "endpoint-invalid";

    // HTTP outcomes
    public const string AgentRejectedCredentials = "agent-rejected-credentials"; // 401/403
    public const string AgentNotFound            = "agent-not-found";            // 404
    public const string AgentRejected            = "agent-rejected";             // other 4xx
    public const string AgentError               = "agent-error";                // 5xx after retries

    // Transport / resilience
    public const string Timeout        = "timeout";
    public const string CircuitOpen    = "circuit-open";
    public const string TransportError = "transport-error";

    // Protocol
    public const string ProtocolError = "protocol-error"; // 2xx body fails to deserialize

    // Cancellation
    public const string Cancelled = "cancelled";
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilGatewayErrorsTests"
```

Expected: 1 test passes.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Gateway/SigilGatewayErrors.cs tests/Sigil.Core.Tests/Gateway/SigilGatewayErrorsTests.cs
git commit -m "feat(core): add SigilGatewayErrors constants"
```

---

## Task 5: `AgentGatewayOptions`

**Files:**
- Create: `src/Sigil.Infrastructure/Gateway/AgentGatewayOptions.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOptionsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOptionsTests.cs`:

```csharp
using Shouldly;
using Sigil.Infrastructure.Gateway;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var opts = new AgentGatewayOptions();

        opts.ValidateTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        opts.ExecuteTimeout.ShouldBe(TimeSpan.FromSeconds(120));
        opts.MaxRetryAttempts.ShouldBe(2);
        opts.BaseRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
        opts.CircuitBreakerFailureRatio.ShouldBe(50);
        opts.CircuitBreakerMinimumThroughput.ShouldBe(10);
        opts.CircuitBreakerSamplingDuration.ShouldBe(TimeSpan.FromSeconds(30));
        opts.CircuitBreakerBreakDuration.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void SectionName_Is_Gateway()
    {
        AgentGatewayOptions.SectionName.ShouldBe("Gateway");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayOptionsTests"
```

Expected: build error — `AgentGatewayOptions` does not exist.

- [ ] **Step 3: Create the options class**

Create `src/Sigil.Infrastructure/Gateway/AgentGatewayOptions.cs`:

```csharp
namespace Sigil.Infrastructure.Gateway;

public sealed class AgentGatewayOptions
{
    public const string SectionName = "Gateway";

    public TimeSpan ValidateTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ExecuteTimeout  { get; set; } = TimeSpan.FromSeconds(120);

    public int      MaxRetryAttempts { get; set; } = 2;
    public TimeSpan BaseRetryDelay   { get; set; } = TimeSpan.FromMilliseconds(200);

    public int      CircuitBreakerFailureRatio      { get; set; } = 50;     // percent
    public int      CircuitBreakerMinimumThroughput { get; set; } = 10;     // calls in window
    public TimeSpan CircuitBreakerSamplingDuration  { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration     { get; set; } = TimeSpan.FromSeconds(15);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayOptionsTests"
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGatewayOptions.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOptionsTests.cs
git commit -m "feat(infra): add AgentGatewayOptions"
```

---

## Task 6: `FakeHttpMessageHandler` + `GatewayTestHarness`

**Files:**
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/FakeHttpMessageHandler.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs`

Two reusable test doubles. Tasks 7–14 use them everywhere; we commit them together so the next task's "failing test" only adds new behavior, not new infrastructure.

The harness intentionally has only the `WithRawClient` builder for now. The DI-built variant (`WithResilience`) is added in Task 12 when the DI extension exists.

- [ ] **Step 1: Create `FakeHttpMessageHandler`**

Create `tests/Sigil.Infrastructure.Tests/Gateway/FakeHttpMessageHandler.cs`:

```csharp
using System.Net;

namespace Sigil.Infrastructure.Tests.Gateway;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _scripted = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void EnqueueResponse(HttpStatusCode status, string? body = null, string mediaType = "application/json")
        => _scripted.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType);
            return response;
        });

    public void EnqueueException(Exception exception)
        => _scripted.Enqueue(_ => throw exception);

    public void EnqueueDelay(TimeSpan delay, HttpStatusCode finalStatus = HttpStatusCode.OK, string? body = null)
        => _scripted.Enqueue(req =>
        {
            // Synchronous delay is fine for tests against Polly's FakeTimeProvider or
            // when the test explicitly cancels. Avoid in tests that exceed CI budgets.
            Thread.Sleep(delay);
            var response = new HttpResponseMessage(finalStatus);
            if (body is not null)
                response.Content = new StringContent(body);
            return response;
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Capture a snapshot. Cloning the body is awkward; tests inspect the
        // original request object so we just record the reference.
        Requests.Add(request);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);

        if (_scripted.Count == 0)
            throw new InvalidOperationException(
                $"FakeHttpMessageHandler received an unexpected request to {request.RequestUri}; " +
                "queue an EnqueueResponse / EnqueueException for every expected call.");

        var produce = _scripted.Dequeue();
        return Task.FromResult(produce(request));
    }
}
```

- [ ] **Step 2: Create `GatewayTestHarness`**

Create `tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs`:

```csharp
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
        gateway  ??= new AgentGatewayOptions();

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
```

> The `AgentGateway` constructor signature this harness assumes is finalised in Task 7. If a later task changes the constructor, update the harness in the same commit.

- [ ] **Step 3: Verify the test project builds**

Both files reference `AgentGateway`, which doesn't exist yet — that's expected. Don't build the solution; the next task introduces `AgentGateway` and brings it into compile range.

```bash
git status
```

Expected: two new untracked files under `tests/Sigil.Infrastructure.Tests/Gateway/`.

- [ ] **Step 4: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Gateway/FakeHttpMessageHandler.cs tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs
git commit -m "test(infra): scaffold FakeHttpMessageHandler and GatewayTestHarness"
```

---

## Task 7: `AgentGateway` skeleton + tier dispatch pre-flight

**Files:**
- Create: `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs`

First behavioral test: a Standard- or Trusted-tier agent fails fast with `tier-not-supported` and zero HTTP requests reach the handler.

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs`:

```csharp
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayTierTests
{
    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task NonOpenTier_Fails_With_TierNotSupported_AndMakesNoHttpCall(SecurityTier tier)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(tier: tier);
        var request = new ValidationRequest
        {
            Task = new AgentTask
            {
                JobId = new JobId("job-1"),
                StepId = new StepId("step-1"),
                SkillName = "echo"
            },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TierNotSupported);
        handler.Requests.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayTierTests"
```

Expected: build error — `AgentGateway` does not exist.

- [ ] **Step 3: Create the gateway skeleton with tier dispatch only**

Create `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`:

```csharp
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
        if (agent.Security.Tier != SecurityTier.Open)
            return Task.FromResult(Result.Failure<ValidationResult>(SigilGatewayErrors.TierNotSupported));

        // Remaining pre-flight + HTTP added in subsequent tasks.
        throw new NotImplementedException();
    }

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent,
        AgentExecutionPackage package,
        CancellationToken ct = default)
    {
        if (agent.Security.Tier != SecurityTier.Open)
            return Task.FromResult(Result.Failure<AgentExecutionResult>(SigilGatewayErrors.TierNotSupported));

        throw new NotImplementedException();
    }

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
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayTierTests"
```

Expected: 2 tests pass (Standard + Trusted theory cases).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGateway.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs
git commit -m "feat(infra): add AgentGateway skeleton with tier-not-supported guard"
```

---

## Task 8: `AgentGateway` — outbound-key-missing + endpoint-invalid pre-flight

**Files:**
- Modify: `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`
- Modify: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs` (rename context — covers all pre-flight)

- [ ] **Step 1: Append the failing tests**

Append to `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs`:

```csharp
    [Fact]
    public async Task OpenTier_NoAllowlistEntry_Fails_With_OutboundKeyMissing()
    {
        var handler = new FakeHttpMessageHandler();
        // Allowlist intentionally empty: agent claims Open tier but has no key configured.
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: new SigilSecurityOptions { Mode = SecurityTier.Open });

        var agent = GatewayTestHarness.MakeRegistration(agentId: "echo-agent");
        var request = new ValidationRequest
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.OutboundKeyMissing);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("/just/a/path")]
    public async Task InvalidEndpoint_Fails_With_EndpointInvalid(string endpointUrl)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(endpointUrl: endpointUrl);
        var request = new ValidationRequest
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.EndpointInvalid);
        handler.Requests.ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayTierTests"
```

Expected: the new tests fail — `ValidateAsync` currently throws `NotImplementedException` after the tier check.

- [ ] **Step 3: Add pre-flight checks to the gateway**

In `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`, replace the bodies of `ValidateAsync` and `ExecuteAsync` with calls to a shared pre-flight helper. Add a helper that returns the resolved key on success or a failure code.

Insert this helper as a private member of `AgentGateway`:

```csharp
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
```

Replace the `ValidateAsync` and `ExecuteAsync` bodies with:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayTierTests"
```

Expected: 7 tests pass total (2 tier theory + 1 outbound-key-missing + 4 endpoint-invalid theory).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGateway.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayTierTests.cs
git commit -m "feat(infra): add outbound-key-missing and endpoint-invalid pre-flight"
```

---

## Task 9: `AgentGateway` — happy-path Validate + Execute

**Files:**
- Modify: `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayValidateTests.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayExecuteTests.cs`

This is the biggest task — implements the full HTTP send path for both methods. After this, the gateway can do its job (without resilience).

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayValidateTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayValidateTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "echo"
        },
        AvailableTokenBudget = 1000
    };

    [Fact]
    public async Task HappyPath_Returns_DeserializedValidationResult()
    {
        var handler = new FakeHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new
        {
            canHandle = true,
            estimatedTokens = 650,
            missingTools = Array.Empty<string>(),
            reason = (string?)null
        });
        handler.EnqueueResponse(HttpStatusCode.OK, responseBody);

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.CanHandle.ShouldBeTrue();
        result.Value.EstimatedTokens.ShouldBe(650);
    }

    [Fact]
    public async Task HappyPath_Posts_To_SigilValidate_With_SigilKeyHeader()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        handler.Requests.Count.ShouldBe(1);
        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsoluteUri.ShouldBe($"{EndpointUrl}/sigil/validate");
        request.Headers.GetValues("X-Sigil-Key").ShouldHaveSingleItem().ShouldBe(Key);
        request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task HappyPath_Sends_Request_Body_As_JsonValidationRequest()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        var sentJson = await handler.Requests[0].Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sentJson);
        var root = doc.RootElement;
        root.GetProperty("task").GetProperty("jobId").GetString().ShouldBe("job-1");
        root.GetProperty("task").GetProperty("stepId").GetString().ShouldBe("step-1");
        root.GetProperty("task").GetProperty("skillName").GetString().ShouldBe("echo");
        root.GetProperty("availableTokenBudget").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public async Task UnparseableResponseBody_Returns_ProtocolError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "not valid json {");

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Sigil.Core.Gateway.SigilGatewayErrors.ProtocolError);
    }
}
```

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayExecuteTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayExecuteTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static AgentExecutionPackage SamplePackage() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "echo"
        },
        Snapshot = new ContextSnapshot { JobId = new JobId("job-1") },
        ExpectedETag = new ETag("etag-1")
    };

    [Fact]
    public async Task HappyPath_Returns_DeserializedExecutionResult()
    {
        var handler = new FakeHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new
        {
            delta = new { updates = new Dictionary<string, object>(), removals = Array.Empty<string>() },
            logs = Array.Empty<object>(),
            metrics = new { promptTokens = 100, completionTokens = 200, duration = "00:00:01" }
        });
        handler.EnqueueResponse(HttpStatusCode.OK, responseBody);

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ExecuteAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SamplePackage());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Metrics.PromptTokens.ShouldBe(100);
    }

    [Fact]
    public async Task HappyPath_Posts_To_SigilExecute()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            delta = new { updates = new Dictionary<string, object>(), removals = Array.Empty<string>() },
            logs = Array.Empty<object>(),
            metrics = new { promptTokens = 0, completionTokens = 0, duration = "00:00:00" }
        }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ExecuteAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SamplePackage());

        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe($"{EndpointUrl}/sigil/execute");
        handler.Requests[0].Headers.GetValues("X-Sigil-Key").ShouldHaveSingleItem().ShouldBe(Key);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGateway"
```

Expected: build error (test references types/members on `AgentGateway` that throw `NotImplementedException`); the new tests fail when reached.

- [ ] **Step 3: Implement the HTTP send path**

In `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`, replace the `ValidateAsync` and `ExecuteAsync` bodies and add a private generic dispatcher. The full updated file body for the methods:

```csharp
    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default)
        => DispatchAsync<ValidationRequest, ValidationResult>(
            agent, request, subPath: "/sigil/validate", method: "validate", ct);

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default)
        => DispatchAsync<AgentExecutionPackage, AgentExecutionResult>(
            agent, package, subPath: "/sigil/execute", method: "execute", ct);

    private async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(
        AgentRegistration agent, TRequest body, string subPath, string method, CancellationToken ct)
    {
        var pre = Preflight(agent);
        if (pre.IsFailure)
            return Result.Failure<TResponse>(pre.Error);

        var requestUri = ComposeEndpoint(pre.Value.BaseUri, subPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("X-Sigil-Key", pre.Value.OutboundKey);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            return await MapResponseAsync<TResponse>(response, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.Cancelled);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.TransportError);
        }
    }

    private static Uri ComposeEndpoint(Uri baseUri, string subPath)
    {
        var basePath = baseUri.AbsoluteUri.TrimEnd('/');
        return new Uri(basePath + subPath, UriKind.Absolute);
    }

    private static async Task<Result<TResponse>> MapResponseAsync<TResponse>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;

        if (status >= 200 && status < 300)
        {
            try
            {
                var deserialized = await response.Content
                    .ReadFromJsonAsync<TResponse>(JsonOptions, ct)
                    .ConfigureAwait(false);
                if (deserialized is null)
                    return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
                return Result.Success(deserialized);
            }
            catch (JsonException)
            {
                return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
            }
            catch (NotSupportedException)
            {
                return Result.Failure<TResponse>(SigilGatewayErrors.ProtocolError);
            }
        }

        return status switch
        {
            401 or 403 => Result.Failure<TResponse>(SigilGatewayErrors.AgentRejectedCredentials),
            404        => Result.Failure<TResponse>(SigilGatewayErrors.AgentNotFound),
            >= 400 and < 500 => Result.Failure<TResponse>(SigilGatewayErrors.AgentRejected),
            _ => Result.Failure<TResponse>(SigilGatewayErrors.AgentError),
        };
    }
```

Add the necessary `using` directives at the top of the file:

```csharp
using System.Net.Http.Json;
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGateway"
```

Expected: all gateway tests pass — 7 (tier) + 4 (validate) + 2 (execute) = 13.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGateway.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayValidateTests.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayExecuteTests.cs
git commit -m "feat(infra): implement AgentGateway HTTP dispatch for validate/execute"
```

---

## Task 10: HTTP outcome mapping — failure cases

**Files:**
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOutcomeTests.cs`

The mapping logic is already in place from Task 9. This task pins each non-2xx response to the right error code and confirms the no-retry behavior on 4xx (verified by request count = 1 — there's no retry handler in the raw-client harness).

- [ ] **Step 1: Write the tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOutcomeTests.cs`:

```csharp
using System.Net;
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayOutcomeTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo"
        },
        AvailableTokenBudget = 1000
    };

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SigilGatewayErrors.AgentRejectedCredentials)]
    [InlineData(HttpStatusCode.Forbidden,    SigilGatewayErrors.AgentRejectedCredentials)]
    [InlineData(HttpStatusCode.NotFound,     SigilGatewayErrors.AgentNotFound)]
    [InlineData(HttpStatusCode.BadRequest,   SigilGatewayErrors.AgentRejected)]
    [InlineData(HttpStatusCode.Conflict,     SigilGatewayErrors.AgentRejected)]
    [InlineData(HttpStatusCode.InternalServerError, SigilGatewayErrors.AgentError)]
    [InlineData(HttpStatusCode.BadGateway,         SigilGatewayErrors.AgentError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SigilGatewayErrors.AgentError)]
    public async Task HttpStatus_Maps_To_ExpectedErrorCode(HttpStatusCode status, string expectedCode)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(status);
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedCode);
        // No retries in the raw-client harness — the handler must have seen exactly one request.
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HttpRequestException_Returns_TransportError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TransportError);
    }

    [Fact]
    public async Task CancelledToken_Returns_Cancelled()
    {
        var handler = new FakeHttpMessageHandler();
        // Don't enqueue a response — the cancellation should fire before SendAsync touches the queue.
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest(),
            cts.Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.Cancelled);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayOutcomeTests"
```

Expected: 10 tests pass (8 status-code theory + 1 transport + 1 cancellation).

If the cancellation test hangs or fails: the gateway needs to short-circuit on a pre-cancelled token. The `try/catch (OperationCanceledException) when (ct.IsCancellationRequested)` in Task 9 already handles this — `_http.SendAsync` checks the token on entry and throws `TaskCanceledException` (which derives from `OperationCanceledException`).

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayOutcomeTests.cs
git commit -m "test(infra): pin AgentGateway HTTP outcome mapping"
```

---

## Task 11: Endpoint composition (path prefix + trailing slash)

**Files:**
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayEndpointTests.cs`

Two specific concerns from spec §5.6: a `EndpointUrl` with a trailing slash, and a `EndpointUrl` with a path prefix (e.g., `https://host/v1`). Both should compose correctly into `{endpoint}/sigil/validate`.

- [ ] **Step 1: Write the tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayEndpointTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayEndpointTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
        AvailableTokenBudget = 1000
    };

    [Theory]
    [InlineData("http://echo-agent:8080",     "http://echo-agent:8080/sigil/validate")]
    [InlineData("http://echo-agent:8080/",    "http://echo-agent:8080/sigil/validate")]
    [InlineData("http://echo-agent:8080/v1",  "http://echo-agent:8080/v1/sigil/validate")]
    [InlineData("http://echo-agent:8080/v1/", "http://echo-agent:8080/v1/sigil/validate")]
    [InlineData("https://agent.example.com/api/v2", "https://agent.example.com/api/v2/sigil/validate")]
    public async Task EndpointUrl_Composes_With_SubPath(string endpointUrl, string expectedAbsoluteUri)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, endpointUrl),
            SampleRequest());

        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe(expectedAbsoluteUri);
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayEndpointTests"
```

Expected: 5 tests pass.

If any case fails because `new Uri(...)` re-encodes characters: adjust `ComposeEndpoint` in `AgentGateway.cs` to use string concatenation against `baseUri.AbsoluteUri.TrimEnd('/')` as written in Task 9. The Task 9 implementation already handles all five cases.

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayEndpointTests.cs
git commit -m "test(infra): pin AgentGateway endpoint composition"
```

---

## Task 12: `AddAgentGateway` DI extension + breaker registry

**Files:**
- Create: `src/Sigil.Infrastructure/Gateway/ServiceCollectionExtensions.cs`
- Modify: `tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs` (add `WithResilience`)
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AddAgentGatewayTests.cs`

Wire up the typed `HttpClient`, the two named resilience handlers (`agent-validate`, `agent-execute`), and the per-agent breaker registry. The DI test confirms `IAgentGateway` resolves to `AgentGateway` and that `AgentGatewayOptions` binds from configuration.

> **Implementation-time check:** confirm the `AddResiliencePipelineRegistry<string>` overload signature against the installed `Polly.Extensions` version. The snippet below assumes the `(IServiceCollection, Action<ResiliencePipelineRegistryOptions<string>, IServiceProvider>)` overload exists; if it doesn't, switch to `IConfigureOptions<ResiliencePipelineRegistryOptions<string>>` and update the spec §5.2 in the same commit.

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AddAgentGatewayTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AddAgentGatewayTests"
```

Expected: build error — `AddAgentGateway` does not exist.

- [ ] **Step 3: Create the DI extension**

Create `src/Sigil.Infrastructure/Gateway/ServiceCollectionExtensions.cs`:

```csharp
using System.Net.Http;
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

        services.AddHttpClient<AgentGateway>()
            .AddResilienceHandler("agent-validate", BuildValidatePipeline)
            .AddResilienceHandler("agent-execute",  BuildExecutePipeline);

        services.AddResiliencePipelineRegistry<string>((options, sp) =>
        {
            var gatewayOpts = sp.GetRequiredService<IOptions<AgentGatewayOptions>>().Value;
            options.BuilderFactory = key => new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddCircuitBreaker(BuildBreakerOptions(gatewayOpts));
        });

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

    private static CircuitBreakerStrategyOptions<HttpResponseMessage> BuildBreakerOptions(AgentGatewayOptions opts) => new()
    {
        FailureRatio       = opts.CircuitBreakerFailureRatio / 100.0,
        MinimumThroughput  = opts.CircuitBreakerMinimumThroughput,
        SamplingDuration   = opts.CircuitBreakerSamplingDuration,
        BreakDuration      = opts.CircuitBreakerBreakDuration,
        ShouldHandle       = static args => ValueTask.FromResult(IsTransient(args.Outcome)),
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
```

> If the `AddResiliencePipelineRegistry<string>((options, sp) => ...)` overload doesn't exist, replace this block with the alternative noted at the top of this task. Update the spec §5.2 snippet in the same commit.

- [ ] **Step 4: Add `WithResilience` to the harness**

Append to `tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs` (above the closing `}` of the static class):

```csharp
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

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>),
                          typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));

        services.AddSigilSecurity(configuration);
        services.AddAgentGateway(configuration);

        // Replace the primary handler on the AgentGateway typed-client with the fake.
        services.AddHttpClient<AgentGateway>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IAgentGateway>();
        return (resolved, provider);
    }
```

Add the missing using if not already present:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sigil.Infrastructure.Gateway;
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AddAgentGatewayTests"
```

Expected: 3 tests pass.

If `Registers_ResiliencePipelineProvider_For_PerAgent_Breakers` fails on `pipelineA.ShouldNotBeSameAs(pipelineB)`: the registry is reusing a single pipeline. Confirm `BuilderFactory` is set (lazy per-key creation) rather than a single pre-built pipeline registered once.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/ServiceCollectionExtensions.cs tests/Sigil.Infrastructure.Tests/Gateway/GatewayTestHarness.cs tests/Sigil.Infrastructure.Tests/Gateway/AddAgentGatewayTests.cs
git commit -m "feat(infra): add AddAgentGateway DI extension with per-agent breakers"
```

---

## Task 13: Per-agent breaker integration in the gateway + resilience tests

**Files:**
- Modify: `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayResilienceTests.cs`

The gateway currently calls `_http.SendAsync` directly. Wire the per-agent breaker pipeline around the call and confirm: (a) 5xx triggers retries up to `MaxRetryAttempts` then surfaces `agent-error`; (b) 4xx is not retried; (c) the breaker opens after threshold and returns `circuit-open`; (d) two distinct agents have isolated breakers; (e) `circuit-open` is surfaced as `SigilGatewayErrors.CircuitOpen`.

- [ ] **Step 1: Wrap the breaker around `_http.SendAsync`**

In `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`, change `DispatchAsync` to resolve the breaker pipeline and execute the send through it. Replace the `try { using var response = ... }` block with:

```csharp
        var breaker = _breakers.GetPipeline<HttpResponseMessage>(
            $"agent-circuit::{agent.AgentId.Value}");

        try
        {
            using var response = await breaker.ExecuteAsync(
                static async (state, ct) =>
                    await state.Http.SendAsync(state.Request, ct).ConfigureAwait(false),
                (Http: _http, Request: request),
                ct).ConfigureAwait(false);

            return await MapResponseAsync<TResponse>(response, ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.CircuitOpen);
        }
        catch (TimeoutRejectedException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.Timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.Cancelled);
        }
        catch (HttpRequestException)
        {
            return Result.Failure<TResponse>(SigilGatewayErrors.TransportError);
        }
```

Add the missing usings to `AgentGateway.cs`:

```csharp
using Polly.CircuitBreaker;
using Polly.Timeout;
```

> The `breaker.ExecuteAsync` call uses the no-state-allocation overload that takes a `(state, ct) =>` lambda + a state tuple. This avoids an allocation per dispatch.

- [ ] **Step 2: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayResilienceTests.cs`:

```csharp
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
        // 1 + MaxRetryAttempts (2) = 3 total attempts before surrendering.
        handler.EnqueueResponse(HttpStatusCode.InternalServerError);
        handler.EnqueueResponse(HttpStatusCode.InternalServerError);
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
        handler.Requests.Count.ShouldBe(3);
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

        // Agent A: 5xx repeatedly to trip its breaker.
        // We expect MinimumThroughput=4 attempts (2 attempts * 2 retries each = 6 total
        // 5xx responses across two sick calls). After the breaker trips for A, calls
        // to A return circuit-open. Calls to B continue to succeed.
        for (int i = 0; i < 8; i++)
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

        // Drive agent-a's breaker open
        for (int i = 0; i < 2; i++)
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
```

- [ ] **Step 3: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayResilienceTests"
```

Expected: 3 tests pass.

If the breaker isolation test fails because agent-a's failures also trip agent-b's breaker: confirm `_breakers.GetPipeline` is keyed by `agent.AgentId.Value` and that the registry's `BuilderFactory` is lazy per-key (not a single shared pipeline).

If the retry-count assertion fails (`handler.Requests.Count.ShouldBe(3)` returns more or fewer): inspect the `MaxRetryAttempts` semantic in the installed `Microsoft.Extensions.Http.Resilience` version. Some versions count "max retries" inclusive of the initial attempt; others don't. Adjust the assertion to match the documented semantic and note the discrepancy in the commit.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGateway.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayResilienceTests.cs
git commit -m "feat(infra): wrap per-agent circuit breaker around HTTP send"
```

---

## Task 14: Activity emission

**Files:**
- Modify: `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayActivityTests.cs`

The static `ActivitySource` already exists from Task 7. This task starts an activity for each dispatch, sets the standard tags from spec §6.1, and confirms a listener can capture them.

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayActivityTests.cs`:

```csharp
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Infrastructure.Gateway;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayActivityTests
{
    [Fact]
    public async Task ValidateAsync_Emits_Activity_With_StandardTags()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Sigil.Gateway",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration("echo-agent", "http://echo-agent:8080"),
            new ValidationRequest
            {
                Task = new AgentTask
                {
                    JobId = new JobId("job-1"),
                    StepId = new StepId("step-1"),
                    SkillName = "echo"
                },
                AvailableTokenBudget = 1000
            });

        var activity = stopped.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("agent.validate");
        activity.GetTagItem("sigil.agent.id").ShouldBe("echo-agent");
        activity.GetTagItem("sigil.agent.endpoint").ShouldBe("http://echo-agent:8080");
        activity.GetTagItem("sigil.agent.tier").ShouldBe("Open");
        activity.GetTagItem("sigil.gateway.method").ShouldBe("validate");
        activity.GetTagItem("sigil.job.id").ShouldBe("job-1");
        activity.GetTagItem("sigil.step.id").ShouldBe("step-1");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGatewayActivityTests"
```

Expected: test fails — no activity is captured (the `ActivitySource` exists but no `StartActivity` is called).

- [ ] **Step 3: Add activity emission**

In `src/Sigil.Infrastructure/Gateway/AgentGateway.cs`, modify `DispatchAsync` to start an activity at the top, set tags after pre-flight, and set the status before returning. Replace the method with:

```csharp
    private async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(
        AgentRegistration agent, TRequest body, string subPath, string method, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity($"agent.{method}", ActivityKind.Client);
        activity?.SetTag("sigil.agent.id", agent.AgentId.Value);
        activity?.SetTag("sigil.agent.endpoint", agent.EndpointUrl);
        activity?.SetTag("sigil.agent.tier", agent.Security.Tier.ToString());
        activity?.SetTag("sigil.gateway.method", method);
        SetTaskTags(activity, body);

        var pre = Preflight(agent);
        if (pre.IsFailure)
        {
            activity?.SetTag("sigil.gateway.error_code", pre.Error);
            activity?.SetStatus(ActivityStatusCode.Error);
            return Result.Failure<TResponse>(pre.Error);
        }

        var requestUri = ComposeEndpoint(pre.Value.BaseUri, subPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("X-Sigil-Key", pre.Value.OutboundKey);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        var breaker = _breakers.GetPipeline<HttpResponseMessage>(
            $"agent-circuit::{agent.AgentId.Value}");

        try
        {
            using var response = await breaker.ExecuteAsync(
                static async (state, ct) =>
                    await state.Http.SendAsync(state.Request, ct).ConfigureAwait(false),
                (Http: _http, Request: request),
                ct).ConfigureAwait(false);

            activity?.SetTag("http.response.status_code", (int)response.StatusCode);

            var outcome = await MapResponseAsync<TResponse>(response, ct).ConfigureAwait(false);
            if (outcome.IsFailure)
            {
                activity?.SetTag("sigil.gateway.error_code", outcome.Error);
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            return outcome;
        }
        catch (BrokenCircuitException)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.CircuitOpen);
        }
        catch (TimeoutRejectedException)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.Timeout);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.Cancelled);
        }
        catch (HttpRequestException)
        {
            return FailWith<TResponse>(activity, SigilGatewayErrors.TransportError);
        }
    }

    private static Result<T> FailWith<T>(Activity? activity, string code)
    {
        activity?.SetTag("sigil.gateway.error_code", code);
        activity?.SetStatus(ActivityStatusCode.Error);
        return Result.Failure<T>(code);
    }

    private static void SetTaskTags<TBody>(Activity? activity, TBody body)
    {
        if (activity is null) return;
        switch (body)
        {
            case ValidationRequest vr:
                activity.SetTag("sigil.job.id",  vr.Task.JobId.Value);
                activity.SetTag("sigil.step.id", vr.Task.StepId.Value);
                break;
            case AgentExecutionPackage pkg:
                activity.SetTag("sigil.job.id",  pkg.Task.JobId.Value);
                activity.SetTag("sigil.step.id", pkg.Task.StepId.Value);
                break;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes and the rest of the suite still does**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AgentGateway"
```

Expected: every gateway test passes — tier (7) + outcome (10) + endpoint (5) + validate (4) + execute (2) + DI (3) + resilience (3) + activity (1) = 35.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Gateway/AgentGateway.cs tests/Sigil.Infrastructure.Tests/Gateway/AgentGatewayActivityTests.cs
git commit -m "feat(infra): emit Activity spans on AgentGateway dispatch"
```

---

## Task 15: Final verification gate

**Files:** none (verification only)

- [ ] **Step 1: Full solution build**

```bash
dotnet build sigil.sln
```

Expected: clean build, zero warnings (any warning would fail the build under `TreatWarningsAsErrors=true`).

- [ ] **Step 2: Full test run**

```bash
dotnet test sigil.sln
```

Expected: every project's tests pass. The `Sigil.Infrastructure.Tests` project should report:

- Existing #4 tests (security): ~22
- `AgentGatewayOptionsTests` — 2 tests
- `AgentGatewayTierTests` — 7 tests (2 tier + 1 outbound-key + 4 endpoint)
- `AgentGatewayValidateTests` — 4 tests
- `AgentGatewayExecuteTests` — 2 tests
- `AgentGatewayOutcomeTests` — 10 tests (8 status + transport + cancelled)
- `AgentGatewayEndpointTests` — 5 tests
- `AddAgentGatewayTests` — 3 tests
- `AgentGatewayResilienceTests` — 3 tests
- `AgentGatewayActivityTests` — 1 test

`Sigil.Core.Tests` adds:
- `SigilGatewayErrorsTests` — 1 test

- [ ] **Step 3: Verify no stray files**

```bash
git status
```

Expected: clean working tree (everything committed across Tasks 1–14).

- [ ] **Step 4: PR description prep**

The acceptance checklist from the issue (spec §11) should be marked complete in the PR description, not amended into commits. No final commit needed for this task — it's a verification gate only.

---

## Self-Review Notes

The plan was checked against the spec section by section:

| Spec section | Coverage |
|---|---|
| §1 Goal | Tasks 3–14 deliver the gateway surface |
| §2 In scope: Core gateway types | Tasks 3 (interface), 4 (errors) |
| §2 In scope: Infrastructure gateway impl | Tasks 5, 7–14 |
| §2 In scope: package addition | Tasks 1, 2 |
| §2 In scope: tests | Tasks 4, 5, 6, 7–14 |
| §3 Q1 (rename to `IAgentGateway`) | Task 3 |
| §3 Q2 (pass `AgentRegistration`) | Task 3 |
| §3 Q3 (same allowlist for outbound) | Task 8 (`Preflight` reads `_security.CurrentValue.OpenTier.Keys`) |
| §3 Q4 (per-agent breaker via registry) | Task 12 (registry) + Task 13 (gateway integration) |
| §3 Q5 (validate/execute distinct timeouts) | Task 12 (named handlers) |
| §3 Q6 (fail fast on non-Open tier) | Task 7 |
| §3 Q7 (`Result<T>` + kebab-case codes) | Task 4 (codes) + every method signature |
| §3 Q8 (cancellation → `Cancelled` not throw) | Task 9 (initial), Task 13 (post-breaker), Task 14 (with activity) |
| §3 Q9 (retry transient only) | Task 12 (`IsTransient` predicate) |
| §3 Q10 (breaker on transient only) | Task 12 (same predicate on breaker) |
| §3 Q11 (endpoint composition) | Tasks 8 (validation) + 9 (`ComposeEndpoint`) + 11 (path-prefix tests) |
| §3 Q12 (static `JsonSerializerOptions`) | Task 7 (`BuildJsonOptions`) |
| §3 Q13 (Activity + ILogger) | Tasks 7 (ActivitySource), 14 (emission). ILogger is wired but not heavily asserted — Polly emits its own retry telemetry per spec §6.2. |
| §4 Contracts | Tasks 3, 4 |
| §5.1 Options | Task 5 |
| §5.2 DI extension | Task 12 |
| §5.3 Gateway class shape | Task 7 (skeleton) → Task 14 (final) |
| §5.4 Dispatch flow | Task 9 (HTTP path) → Task 13 (breaker) → Task 14 (activity) |
| §5.5 HTTP outcome mapping | Task 9 (implementation) + Task 10 (tests) |
| §5.6 Endpoint composition | Tasks 9, 11 |
| §5.7 JSON | Task 7 |
| §6.1 Activity tags | Task 14 |
| §6.2 Logging | The framework is in place (`ILogger<AgentGateway>` injected); concrete log-level assertions are deferred — Polly's own telemetry, plus Activity tags, give consumers what they need. |
| §7 Tests | Tasks 4, 5, 6, 7–14 (every scenario in §7.1's matrix is covered) |
| §8 Configuration sample | Task 12 (binding test) |
| §9 Dependencies | Tasks 1, 2 |
| §10 Open questions | Pre-flight notes call out the registry-callback API resolution; Task 12 instructions handle the fallback path |
| §11 Acceptance checklist | All deliverables mapped above |

### Type/method consistency check

- `IAgentGateway.ValidateAsync` / `ExecuteAsync` — same signature in Task 3, 6, 7, 8, 9, 12, 13, 14. ✓
- `SigilGatewayErrors.*` constants — names match across Tasks 4, 7, 8, 9, 10, 13, 14. ✓
- `AgentGatewayOptions` property names — match across Tasks 5, 12, 13. ✓
- `GatewayTestHarness.MakeRegistration(agentId, endpointUrl, tier)` — signature stable across Tasks 7–14. ✓
- `_breakers.GetPipeline<HttpResponseMessage>($"agent-circuit::{agent.AgentId.Value}")` — same key format in Tasks 12 (registry), 13 (lookup), 14 (lookup). ✓
- `JsonSerializerOptions` — built once in Task 7; reused (not redefined) in Tasks 9, 14. ✓

### Placeholder scan

No "TODO", "TBD", or "implement later" steps. Every code-modifying step shows the full code. Implementation-time API uncertainties (Task 1 version selection, Task 12 registry-callback overload, Task 13 retry-count semantic) are flagged with explicit fallback instructions, not deferred.

---

## Execution Handoff

Plan complete and saved to `.bob/plans/2026-05-09-issue-10-agent-gateway-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
