# Issue #13 — FastEndpoints agent lifecycle + intent: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose the kernel's first HTTP API — four agent-lifecycle endpoints (`register` / `deregister` / `heartbeat` / `list`) and a synchronous intent-dispatch endpoint — all gated by an Open-tier `X-Sigil-Agent-Id` + `X-Sigil-Key` middleware, with intent dispatch flowing through a new `IIntentDispatcher` seam.

**Architecture:** A new `SigilAuthMiddleware` in `Sigil.Api` translates HTTP headers into `SigilCredentials` and authenticates via `ISigilSecurity`. FastEndpoints classes under `Sigil.Api/Endpoints/Agents/` and `Sigil.Api/Endpoints/Intents/` delegate to existing `IAgentRegistry` (lifecycle) or a new `IIntentDispatcher` (intents). The dispatcher (`SimpleIntentDispatcher` in `Sigil.Runtime`) chains `SelectByWeightAsync → IAgentGateway.ValidateAsync → ExecuteAsync`. Phase 2's planner-driven orchestrator will swap the dispatcher without touching the endpoint.

**Tech Stack:** .NET 9, FastEndpoints v8, xunit + Shouldly, CSharpFunctionalExtensions, Microsoft.AspNetCore.Mvc.Testing (new), Polly (already used by gateway), EF Core (Postgres via existing `AddSigilEfCore`).

**Spec:** `docs/superpowers/specs/2026-05-17-issue-13-fastendpoints-agent-lifecycle-intent-design.md`

---

## Spec corrections (baked into this plan)

While exploring the codebase, several spec details were found to differ from the actual types in the repo. The plan below uses the **correct** facts; the spec text will be patched in Task 0 to match.

| Spec said | Reality (use this) |
|---|---|
| `ValidationRequest { SkillName, Snapshot, RequestedTokenBudget }` | `ValidationRequest { AgentTask Task, int AvailableTokenBudget }` |
| `ValidationResult.Accepted` | `ValidationResult.CanHandle` |
| `AgentTask.Input` is `JsonElement` | `AgentTask.Input` is `string` |
| `ContextSnapshot.Empty` | No `.Empty` — construct `new ContextSnapshot { JobId = jobId }` |
| `AgentId.Parse(value)` | No `Parse` — use `new AgentId(value)` |
| `AddSigilEfCoreStorage` | Extension is named `AddSigilEfCore` |
| Test packages: `FluentAssertions`, `NSubstitute` | Repo uses `Shouldly` + hand-rolled fakes |
| Error codes `snake_case` | Repo convention is `kebab-case` (e.g. `agent-not-found`, `unknown-agent`) |
| `JobId.New()` | No factory — use `new JobId(Guid.NewGuid().ToString())` |
| `AgentExecutionPackage` was missing `ExpectedETag` in spec | `ExpectedETag` is required — Phase-1 stub uses `new ETag("")` |

---

## File structure

**New source files**

- `src/Sigil.Core/Intents/IIntentDispatcher.cs` — interface + `IntentRequest` record
- `src/Sigil.Core/Intents/IntentErrors.cs` — kebab-case string constants
- `src/Sigil.Runtime/Intents/SimpleIntentDispatcher.cs` — Phase-1 chain implementation
- `src/Sigil.Api/Security/SigilAuthMiddleware.cs` — `X-Sigil-Agent-Id` + `X-Sigil-Key` gate
- `src/Sigil.Api/Security/SigilAuthExtensions.cs` — `HttpContext.GetAuthenticatedAgentId()` helper
- `src/Sigil.Api/Endpoints/Agents/RegisterEndpoint.cs`
- `src/Sigil.Api/Endpoints/Agents/DeregisterEndpoint.cs`
- `src/Sigil.Api/Endpoints/Agents/HeartbeatEndpoint.cs`
- `src/Sigil.Api/Endpoints/Agents/ListAgentsEndpoint.cs`
- `src/Sigil.Api/Endpoints/Intents/SubmitIntentEndpoint.cs`

**Modified source files**

- `src/Sigil.Api/Program.cs` — wire security, EF Core, runtime, middleware
- `src/Sigil.Api/Sigil.Api.csproj` — add project refs to `Sigil.Infrastructure`, `Sigil.Runtime`, `Sigil.Storage.EfCore`
- `src/Sigil.Api/appsettings.Development.json` — sample `Security.OpenTier.Keys` entry
- `src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs` — register `IIntentDispatcher`
- `Directory.Packages.props` — add `Microsoft.AspNetCore.Mvc.Testing`

**New test files**

- `tests/Sigil.Runtime.Tests/Intents/SimpleIntentDispatcherTests.cs`
- `tests/Sigil.Runtime.Tests/Intents/FakeAgentGateway.cs` (in-test fake)
- `tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj` (new project)
- `tests/Sigil.Api.Tests/Infrastructure/SigilApiFactory.cs` — `WebApplicationFactory<Program>` with fakes
- `tests/Sigil.Api.Tests/Infrastructure/FakeAgentRegistrationStore.cs` (duplicated from runtime tests — projects can't reference each other)
- `tests/Sigil.Api.Tests/Infrastructure/StubAgentGateway.cs`
- `tests/Sigil.Api.Tests/Security/SigilAuthMiddlewareTests.cs`
- `tests/Sigil.Api.Tests/Endpoints/Agents/RegisterEndpointTests.cs`
- `tests/Sigil.Api.Tests/Endpoints/Agents/DeregisterEndpointTests.cs`
- `tests/Sigil.Api.Tests/Endpoints/Agents/HeartbeatEndpointTests.cs`
- `tests/Sigil.Api.Tests/Endpoints/Agents/ListAgentsEndpointTests.cs`
- `tests/Sigil.Api.Tests/Endpoints/Intents/SubmitIntentEndpointTests.cs`

**Modified test infra**

- `sigil.sln` — add `Sigil.Api.Tests`

---

## Task 0: Patch the spec to match repo facts

**Files:**
- Modify: `docs/superpowers/specs/2026-05-17-issue-13-fastendpoints-agent-lifecycle-intent-design.md`

- [ ] **Step 1: Open the spec and replace each incorrect fact**

Apply the corrections listed in the "Spec corrections" table above. Specifically:

1. In the `IIntentDispatcher` section, change `IntentRequest.Input` from `JsonElement` to `string` and remove the `using System.Text.Json;` line.
2. In the `SimpleIntentDispatcher` pseudocode, rewrite the `ValidationRequest` construction as:
   ```csharp
   var task = new AgentTask
   {
       JobId = new JobId(Guid.NewGuid().ToString()),
       SkillName = req.SkillName,
       Input = req.Input,
       AvailableTools = agent.Tools.Select(t => t.Name).ToList()
   };
   var snapshot = req.Snapshot ?? new ContextSnapshot { JobId = task.JobId };
   var valReq = new ValidationRequest { Task = task, AvailableTokenBudget = agent.MaxTokenBudget ?? 0 };
   ```
   And change the validation-rejection check from `!valRes.Value.Accepted` to `!valRes.Value.CanHandle`.
3. In the package construction, add `ExpectedETag = new ETag("")` to `AgentExecutionPackage`.
4. In `SubmitIntentRequest`, change `Input` from `JsonElement` to `string`.
5. In `Program.cs` wiring, change `AddSigilEfCoreStorage(...)` to `AddSigilEfCore(...)`.
6. In every error-code reference, switch underscores to dashes: `no-agent-for-skill`, `validation-rejected`, `missing-credentials`, `invalid-agent-id`, `caller-agent-mismatch`, `conflicting-filters`.
7. In the Testing section, replace `FluentAssertions`, `NSubstitute` with `Shouldly` + hand-rolled fakes; note `Microsoft.AspNetCore.Mvc.Testing` will be added to `Directory.Packages.props`.

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/specs/2026-05-17-issue-13-fastendpoints-agent-lifecycle-intent-design.md
git commit -m "docs(spec): correct issue-13 spec to match actual types in repo"
```

---

## Task 1: Add Microsoft.AspNetCore.Mvc.Testing to Central Package Management

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Add the package version**

Add a new `ItemGroup` (or extend the test-only group) in `Directory.Packages.props`:

```xml
  <!-- Test-only — ASP.NET Core integration testing -->
  <ItemGroup>
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
  </ItemGroup>
```

Use version `9.0.0` to match the existing `Microsoft.EntityFrameworkCore` 9.x line — the test host follows the ASP.NET Core version, not the `Microsoft.Extensions.*` 10.x line.

- [ ] **Step 2: Verify it restores**

Run: `dotnet restore sigil.sln`
Expected: completes without errors. (No project consumes the package yet — restore should just resolve the version.)

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "build: add Microsoft.AspNetCore.Mvc.Testing 9.0.0 to CPM"
```

---

## Task 2: `IntentErrors` constants

**Files:**
- Create: `src/Sigil.Core/Intents/IntentErrors.cs`

- [ ] **Step 1: Create the directory and file**

```csharp
namespace Sigil.Core.Intents;

/// <summary>
/// Stable string error codes returned by <see cref="IIntentDispatcher"/>.
/// Consumers (endpoints, logs, tests) match on these values.
/// </summary>
public static class IntentErrors
{
    public const string NoAgentForSkill = "no-agent-for-skill";
    public const string ValidationRejected = "validation-rejected";
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Core/Sigil.Core.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Intents/IntentErrors.cs
git commit -m "feat(core): add IntentErrors constants"
```

---

## Task 3: `IIntentDispatcher` interface + `IntentRequest`

**Files:**
- Create: `src/Sigil.Core/Intents/IIntentDispatcher.cs`

- [ ] **Step 1: Write the file**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Protocol;

namespace Sigil.Core.Intents;

/// <summary>
/// Phase-1 seam between the intent endpoint and the agent gateway.
/// Phase 2 will introduce the planner-driven orchestrator behind the same interface.
/// </summary>
public interface IIntentDispatcher
{
    Task<Result<AgentExecutionResult>> DispatchAsync(
        IntentRequest request,
        CancellationToken ct = default);
}

public sealed record IntentRequest
{
    public required string SkillName { get; init; }
    public required string Input { get; init; }
    public ContextSnapshot? Snapshot { get; init; }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Core/Sigil.Core.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Intents/IIntentDispatcher.cs
git commit -m "feat(core): add IIntentDispatcher interface and IntentRequest"
```

---

## Task 4: `FakeAgentGateway` test helper

**Files:**
- Create: `tests/Sigil.Runtime.Tests/Intents/FakeAgentGateway.cs`

- [ ] **Step 1: Create the fake**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Intents;

internal sealed class FakeAgentGateway : IAgentGateway
{
    public Func<AgentRegistration, ValidationRequest, Result<ValidationResult>> OnValidate { get; set; } =
        (_, _) => Result.Success(new ValidationResult { CanHandle = true });

    public Func<AgentRegistration, AgentExecutionPackage, Result<AgentExecutionResult>> OnExecute { get; set; } =
        (_, _) => Result.Success(new AgentExecutionResult { Delta = new ContextDelta() });

    public List<(AgentRegistration Agent, ValidationRequest Request)> ValidateCalls { get; } = new();
    public List<(AgentRegistration Agent, AgentExecutionPackage Package)> ExecuteCalls { get; } = new();

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default)
    {
        ValidateCalls.Add((agent, request));
        return Task.FromResult(OnValidate(agent, request));
    }

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default)
    {
        ExecuteCalls.Add((agent, package));
        return Task.FromResult(OnExecute(agent, package));
    }
}
```

If `ContextDelta` requires any properties, inspect `src/Sigil.Core/Protocol/ContextDelta.cs` and pass the minimum required (all `required` properties). The current `ContextDelta` is parameterless (verify with `cat src/Sigil.Core/Protocol/ContextDelta.cs`).

- [ ] **Step 2: Verify build**

Run: `dotnet build tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
Expected: build fails because `Sigil.Runtime.Tests` doesn't yet reference `Sigil.Core.Gateway` (the existing reference graph already covers this transitively via `Sigil.Core`). If it succeeds, great. If `IAgentGateway` cannot be found, the runtime tests project needs no new reference (`Sigil.Core` is already there), so a missing `using` is the only failure path — already supplied above.

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Runtime.Tests/Intents/FakeAgentGateway.cs
git commit -m "test(runtime): add FakeAgentGateway helper"
```

---

## Task 5: `SimpleIntentDispatcher` — TDD red phase

**Files:**
- Create: `tests/Sigil.Runtime.Tests/Intents/SimpleIntentDispatcherTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;
using Sigil.Runtime.Intents;
using Sigil.Runtime.Registry;
using Sigil.Runtime.Tests.Registry;

namespace Sigil.Runtime.Tests.Intents;

public sealed class SimpleIntentDispatcherTests
{
    private static AgentRegistration MakeHealthyAgent(string id = "agent-1", string skill = "echo") =>
        new()
        {
            AgentId = new AgentId(id),
            Name = id,
            Domain = "test",
            EndpointUrl = "https://localhost/agent",
            Status = AgentStatus.Healthy,
            Model = new ModelSpec { Provider = "test", Name = "test" },
            Skills = new[] { new Skill { Name = skill, Description = skill } },
        };

    private static (SimpleIntentDispatcher Dispatcher, FakeAgentRegistrationStore Store, FakeAgentGateway Gateway)
        BuildSut()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = new AgentRegistry(store, new StubRandomProvider(0));
        var gateway = new FakeAgentGateway();
        return (new SimpleIntentDispatcher(registry, gateway), store, gateway);
    }

    [Fact]
    public async Task NoAgentForSkill_ReturnsFailure()
    {
        var (sut, _, _) = BuildSut();

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntentErrors.NoAgentForSkill);
    }

    [Fact]
    public async Task GatewayValidateFailure_PropagatesError()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Failure<ValidationResult>("circuit-open");

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("circuit-open");
        gw.ExecuteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationRejected_WithReason_ReturnsReason()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Success(new ValidationResult { CanHandle = false, Reason = "too-many-tokens" });

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("too-many-tokens");
        gw.ExecuteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationRejected_NoReason_ReturnsFallback()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Success(new ValidationResult { CanHandle = false });

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntentErrors.ValidationRejected);
    }

    [Fact]
    public async Task HappyPath_CallsExecuteWithSelectedAgent()
    {
        var (sut, store, gw) = BuildSut();
        var agent = MakeHealthyAgent();
        await store.RegisterAsync(agent);
        // Registry normalizes to Starting; promote to Healthy by hand for selection.
        await store.UpdateStatusAsync(agent.AgentId, AgentStatus.Healthy);

        var expected = new AgentExecutionResult { Delta = new ContextDelta() };
        gw.OnExecute = (_, _) => Result.Success(expected);

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hello" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        gw.ExecuteCalls.Count.ShouldBe(1);
        gw.ExecuteCalls[0].Agent.AgentId.ShouldBe(agent.AgentId);
        gw.ExecuteCalls[0].Package.Task.SkillName.ShouldBe("echo");
        gw.ExecuteCalls[0].Package.Task.Input.ShouldBe("hello");
    }
}
```

If the existing `MakeHealthyAgent` helper exists in `tests/Sigil.Runtime.Tests/Registry/TestAgents.cs`, use it instead of redefining. Verify with `cat tests/Sigil.Runtime.Tests/Registry/TestAgents.cs` and replace `MakeHealthyAgent` calls accordingly.

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
Expected: compilation error — `SimpleIntentDispatcher` does not exist.

---

## Task 6: `SimpleIntentDispatcher` — green phase

**Files:**
- Create: `src/Sigil.Runtime/Intents/SimpleIntentDispatcher.cs`

- [ ] **Step 1: Write the implementation**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Intents;

public sealed class SimpleIntentDispatcher : IIntentDispatcher
{
    private readonly IAgentRegistry _registry;
    private readonly IAgentGateway _gateway;

    public SimpleIntentDispatcher(IAgentRegistry registry, IAgentGateway gateway)
    {
        _registry = registry;
        _gateway = gateway;
    }

    public async Task<Result<AgentExecutionResult>> DispatchAsync(
        IntentRequest request, CancellationToken ct = default)
    {
        var agentMaybe = await _registry.SelectByWeightAsync(request.SkillName, ct);
        if (agentMaybe.HasNoValue)
            return Result.Failure<AgentExecutionResult>(IntentErrors.NoAgentForSkill);

        var agent = agentMaybe.Value;

        var task = new AgentTask
        {
            JobId = new JobId(Guid.NewGuid().ToString()),
            SkillName = request.SkillName,
            Input = request.Input,
            AvailableTools = agent.Tools.Select(t => t.Name).ToList(),
        };

        var snapshot = request.Snapshot ?? new ContextSnapshot { JobId = task.JobId };

        var valReq = new ValidationRequest
        {
            Task = task,
            AvailableTokenBudget = agent.MaxTokenBudget ?? 0,
        };

        var valRes = await _gateway.ValidateAsync(agent, valReq, ct);
        if (valRes.IsFailure)
            return Result.Failure<AgentExecutionResult>(valRes.Error);

        if (!valRes.Value.CanHandle)
            return Result.Failure<AgentExecutionResult>(
                valRes.Value.Reason ?? IntentErrors.ValidationRejected);

        var package = new AgentExecutionPackage
        {
            Task = task,
            Snapshot = snapshot,
            ExpectedETag = new ETag(""),
        };

        return await _gateway.ExecuteAsync(agent, package, ct);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
Expected: all 5 `SimpleIntentDispatcherTests` pass, no other regressions.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Runtime/Intents/SimpleIntentDispatcher.cs tests/Sigil.Runtime.Tests/Intents/
git commit -m "feat(runtime): SimpleIntentDispatcher (Phase 1 stub)"
```

---

## Task 7: Register `IIntentDispatcher` in `AddSigilRuntime`

**Files:**
- Modify: `src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs`

- [ ] **Step 1: Add the registration**

Add `using Sigil.Core.Intents;` and `using Sigil.Runtime.Intents;`, then inside `AddSigilRuntime` after the registry line add:

```csharp
services.AddScoped<IIntentDispatcher, SimpleIntentDispatcher>();
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs
git commit -m "feat(runtime): register IIntentDispatcher in AddSigilRuntime"
```

---

## Task 8: Wire `Sigil.Api` project references and `Program.cs` partial

**Files:**
- Modify: `src/Sigil.Api/Sigil.Api.csproj`
- Modify: `src/Sigil.Api/Program.cs`

- [ ] **Step 1: Add project references**

Open `src/Sigil.Api/Sigil.Api.csproj`. Add the following `ItemGroup` (or append to the existing `ProjectReference` group):

```xml
  <ItemGroup>
    <ProjectReference Include="..\Sigil.Core\Sigil.Core.csproj" />
    <ProjectReference Include="..\Sigil.Infrastructure\Sigil.Infrastructure.csproj" />
    <ProjectReference Include="..\Sigil.Runtime\Sigil.Runtime.csproj" />
    <ProjectReference Include="..\Sigil.Storage.EfCore\Sigil.Storage.EfCore.csproj" />
  </ItemGroup>
```

(`Sigil.Core` is transitively available through the other three, but listing it explicitly makes the API project's intent obvious.)

- [ ] **Step 2: Rewrite `Program.cs`**

Replace the entire contents with:

```csharp
using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Infrastructure.Security;
using Sigil.Runtime.DependencyInjection;
using Sigil.Storage.EfCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSigilSecurity(builder.Configuration);
builder.Services.AddSigilEfCore(builder.Configuration);
builder.Services.AddSigilRuntime();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseMiddleware<SigilAuthMiddleware>();
app.UseFastEndpoints();
app.Run();

public partial class Program;
```

(The `Sigil.Api.Security` namespace will be created in Task 9.)

- [ ] **Step 3: Build (expected to fail)**

Run: `dotnet build src/Sigil.Api/Sigil.Api.csproj`
Expected: fails because `SigilAuthMiddleware` does not exist yet. That's fine — Task 9 creates it.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Api/Sigil.Api.csproj src/Sigil.Api/Program.cs
git commit -m "feat(api): wire Sigil.Api project refs and Program.cs bootstrap"
```

---

## Task 9: `SigilAuthMiddleware`

**Files:**
- Create: `src/Sigil.Api/Security/SigilAuthMiddleware.cs`
- Create: `src/Sigil.Api/Security/SigilAuthExtensions.cs`

- [ ] **Step 1: Write the middleware**

```csharp
using System.Text.Json;
using Sigil.Core.Identity;
using Sigil.Core.Security;

namespace Sigil.Api.Security;

public sealed class SigilAuthMiddleware
{
    public const string HeaderAgentId = "X-Sigil-Agent-Id";
    public const string HeaderKey = "X-Sigil-Key";
    public const string AuthContextItemKey = "sigil.auth";
    public const string ErrorMissingCredentials = "missing-credentials";
    public const string ErrorInvalidAgentId = "invalid-agent-id";

    private readonly RequestDelegate _next;

    public SigilAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ISigilSecurity security)
    {
        var agentIdHeader = ctx.Request.Headers[HeaderAgentId].ToString();
        var keyHeader = ctx.Request.Headers[HeaderKey].ToString();

        if (string.IsNullOrWhiteSpace(agentIdHeader) || string.IsNullOrWhiteSpace(keyHeader))
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, ErrorMissingCredentials);
            return;
        }

        if (agentIdHeader.Length is 0 or > 256)
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, ErrorInvalidAgentId);
            return;
        }

        var credentials = new SigilCredentials
        {
            AgentId = new AgentId(agentIdHeader),
            SigilKey = keyHeader,
        };

        var auth = await security.AuthenticateAsync(credentials, SecurityTier.Open, ctx.RequestAborted);
        if (auth.IsFailure)
        {
            await WriteError(ctx, StatusCodes.Status401Unauthorized, auth.Error);
            return;
        }

        ctx.Items[AuthContextItemKey] = auth.Value;
        await _next(ctx);
    }

    private static async Task WriteError(HttpContext ctx, int statusCode, string code)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = code }));
    }
}
```

- [ ] **Step 2: Write the extension helper**

```csharp
using Sigil.Core.Identity;
using Sigil.Core.Security;

namespace Sigil.Api.Security;

public static class SigilAuthExtensions
{
    public static AgentId GetAuthenticatedAgentId(this HttpContext ctx)
    {
        var auth = (AuthenticationResult?)ctx.Items[SigilAuthMiddleware.AuthContextItemKey]
            ?? throw new InvalidOperationException(
                "SigilAuthMiddleware did not run or did not authenticate this request.");
        return auth.AgentId;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Sigil.Api/Sigil.Api.csproj`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Api/Security/
git commit -m "feat(api): SigilAuthMiddleware (Open tier X-Sigil-Key gate)"
```

---

## Task 10: Create the `Sigil.Api.Tests` project + integration test scaffolding

**Files:**
- Create: `tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj`
- Create: `tests/Sigil.Api.Tests/Infrastructure/SigilApiFactory.cs`
- Create: `tests/Sigil.Api.Tests/Infrastructure/FakeAgentRegistrationStore.cs`
- Create: `tests/Sigil.Api.Tests/Infrastructure/StubAgentGateway.cs`
- Create: `tests/Sigil.Api.Tests/Infrastructure/TestKeys.cs`
- Modify: `sigil.sln`

- [ ] **Step 1: Create the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Sigil.Api\Sigil.Api.csproj" />
    <ProjectReference Include="..\..\src\Sigil.Core\Sigil.Core.csproj" />
    <ProjectReference Include="..\..\src\Sigil.Runtime\Sigil.Runtime.csproj" />
    <ProjectReference Include="..\..\src\Sigil.Infrastructure\Sigil.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the in-memory `FakeAgentRegistrationStore`**

Copy verbatim from `tests/Sigil.Runtime.Tests/Registry/FakeAgentRegistrationStore.cs` into the new path, but change the namespace to `Sigil.Api.Tests.Infrastructure` and visibility to `internal`. Duplication is intentional and documented in the existing file's comment (test projects don't reference each other).

- [ ] **Step 3: Create `StubAgentGateway`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Gateway;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Infrastructure;

internal sealed class StubAgentGateway : IAgentGateway
{
    public Func<AgentRegistration, ValidationRequest, Result<ValidationResult>> OnValidate { get; set; } =
        (_, _) => Result.Success(new ValidationResult { CanHandle = true });

    public Func<AgentRegistration, AgentExecutionPackage, Result<AgentExecutionResult>> OnExecute { get; set; } =
        (_, _) => Result.Success(new AgentExecutionResult { Delta = new ContextDelta() });

    public Task<Result<ValidationResult>> ValidateAsync(
        AgentRegistration agent, ValidationRequest request, CancellationToken ct = default) =>
        Task.FromResult(OnValidate(agent, request));

    public Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentRegistration agent, AgentExecutionPackage package, CancellationToken ct = default) =>
        Task.FromResult(OnExecute(agent, package));
}
```

- [ ] **Step 4: Create the shared test keys**

```csharp
namespace Sigil.Api.Tests.Infrastructure;

internal static class TestKeys
{
    public const string AgentA = "agent-a";
    public const string AgentAKey = "key-for-agent-a";
    public const string AgentB = "agent-b";
    public const string AgentBKey = "key-for-agent-b";
}
```

- [ ] **Step 5: Create `SigilApiFactory`**

```csharp
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Core.Gateway;
using Sigil.Core.Storage;

namespace Sigil.Api.Tests.Infrastructure;

internal sealed class SigilApiFactory : WebApplicationFactory<Program>
{
    public FakeAgentRegistrationStore Store { get; } = new();
    public StubAgentGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Mode"] = "Open",
                [$"Security:OpenTier:Keys:{TestKeys.AgentA}"] = TestKeys.AgentAKey,
                [$"Security:OpenTier:Keys:{TestKeys.AgentB}"] = TestKeys.AgentBKey,
                // Postgres connection string is required by SigilEfCoreOptions validation.
                // The EF store is replaced below — value just needs to be non-empty.
                ["Storage:ConnectionString"] = "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the EF Core-backed registration store with the in-memory fake.
            services.RemoveAll(typeof(IAgentRegistrationStore));
            services.AddSingleton<IAgentRegistrationStore>(Store);

            // Replace the HTTP gateway with our stub.
            services.RemoveAll(typeof(IAgentGateway));
            services.AddSingleton<IAgentGateway>(Gateway);
        });
    }

    public HttpClient CreateAuthedClient(string agentId, string key)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigil-Agent-Id", agentId);
        client.DefaultRequestHeaders.Add("X-Sigil-Key", key);
        return client;
    }
}
```

Note `Storage:ConnectionString` matches the EF Core options' validation requirement; the EF store is removed and replaced before the host runs, so the connection is never opened.

- [ ] **Step 6: Add the project to `sigil.sln`**

Run: `dotnet sln sigil.sln add tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj`
Expected: project added.

- [ ] **Step 7: Build**

Run: `dotnet build tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj`
Expected: succeeds. (No endpoints yet, but the factory should compile against the existing API project.)

- [ ] **Step 8: Commit**

```bash
git add tests/Sigil.Api.Tests/ sigil.sln
git commit -m "test(api): scaffold Sigil.Api.Tests with WebApplicationFactory and fakes"
```

---

## Task 11: `SigilAuthMiddleware` integration tests

**Files:**
- Create: `tests/Sigil.Api.Tests/Security/SigilAuthMiddlewareTests.cs`

The tests in this task use the not-yet-built `GET /api/agents` endpoint as a vehicle to reach the middleware. Since that endpoint doesn't exist yet, these tests will fail until Task 15 creates it. That's fine — they're recorded here next to the middleware to keep auth coverage co-located.

- [ ] **Step 1: Write the tests**

```csharp
using System.Net;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;

namespace Sigil.Api.Tests.Security;

public sealed class SigilAuthMiddlewareTests : IClassFixture<SigilApiFactory>
{
    private readonly SigilApiFactory _factory;
    public SigilAuthMiddlewareTests(SigilApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MissingBothHeaders_Returns401_MissingCredentials()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"missing-credentials\"");
    }

    [Fact]
    public async Task MissingKeyOnly_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigil-Agent-Id", TestKeys.AgentA);
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"missing-credentials\"");
    }

    [Fact]
    public async Task UnknownAgent_Returns401()
    {
        var client = _factory.CreateAuthedClient("never-registered", "any-key");
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"unknown-agent\"");
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, "wrong-key");
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"key-mismatch\"");
    }

    [Fact]
    public async Task ValidHeaders_ReachesEndpoint()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run the tests — expect 4 failures (404 on list endpoint) + 1 pass**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter SigilAuthMiddlewareTests`
Expected: the four 401-checking tests pass (middleware blocks before the endpoint is reached); `ValidHeaders_ReachesEndpoint` fails because `/api/agents` returns 404 (no endpoint registered yet).

- [ ] **Step 3: Commit (leaving the one failing test as a tracking smoke for Task 15)**

```bash
git add tests/Sigil.Api.Tests/Security/SigilAuthMiddlewareTests.cs
git commit -m "test(api): SigilAuthMiddleware integration tests"
```

---

## Task 12: `POST /api/agents/register`

**Files:**
- Create: `src/Sigil.Api/Endpoints/Agents/RegisterEndpoint.cs`
- Create: `tests/Sigil.Api.Tests/Endpoints/Agents/RegisterEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class RegisterEndpointTests : IClassFixture<SigilApiFactory>
{
    private readonly SigilApiFactory _factory;
    public RegisterEndpointTests(SigilApiFactory factory) => _factory = factory;

    private static AgentRegistration NewRegistration(string id = TestKeys.AgentA) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Model = new ModelSpec { Provider = "test", Name = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo back" } },
    };

    [Fact]
    public async Task HappyPath_Returns201()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<AgentRegistration>();
        body!.AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration(TestKeys.AgentB));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"caller-agent-mismatch\"");
    }

    [Fact]
    public async Task DuplicateAgent_Returns409()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"duplicate-agent\"");
    }

    [Fact]
    public async Task InvalidRoutingWeight_Returns400()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var bad = NewRegistration() with { RoutingWeight = 999 };

        var res = await client.PostAsJsonAsync("/api/agents/register", bad);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"invalid-routing-weight\"");
    }
}
```

Note: tests that mutate state (`DuplicateAgent`, `InvalidRoutingWeight` for clarity) use a fresh factory to avoid pollution from sibling tests sharing the class-fixture instance.

- [ ] **Step 2: Run tests — expect compilation/404 failures**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter RegisterEndpointTests`
Expected: tests run but all fail with 404 because the endpoint doesn't exist yet.

- [ ] **Step 3: Write the endpoint**

```csharp
using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed class RegisterEndpoint : Endpoint<AgentRegistration, AgentRegistration>
{
    private readonly IAgentRegistry _registry;
    public RegisterEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Post("/api/agents/register");
        AllowAnonymous(); // SigilAuthMiddleware gates this
    }

    public override async Task HandleAsync(AgentRegistration req, CancellationToken ct)
    {
        var caller = HttpContext.GetAuthenticatedAgentId();
        if (caller != req.AgentId)
        {
            await SendErrorAsync(StatusCodes.Status403Forbidden, "caller-agent-mismatch", ct);
            return;
        }

        var result = await _registry.RegisterAsync(req, ct);
        if (result.IsFailure)
        {
            var status = result.Error switch
            {
                RegistryErrors.InvalidRoutingWeight => StatusCodes.Status400BadRequest,
                RegistryErrors.SkillNameRequired => StatusCodes.Status400BadRequest,
                "duplicate-agent" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            await SendErrorAsync(status, result.Error, ct);
            return;
        }

        // RegisterAsync normalizes status to Starting; load the stored copy so the response reflects truth.
        var stored = await _registry.GetAsync(req.AgentId, ct);
        await SendAsync(stored.Value, StatusCodes.Status201Created, ct);
    }

    private async Task SendErrorAsync(int status, string code, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = status;
        await SendAsync(new { error = code } as object, status, ct);
    }
}
```

Note on `SendErrorAsync`: FastEndpoints' `SendAsync<TResponse>` requires the response type. Casting the anonymous `{ error = code }` to `object` is awkward — if the build fails or the response shape is wrong, replace with:

```csharp
await HttpContext.Response.WriteAsJsonAsync(new { error = code }, ct);
```

before `return`. Verify the JSON wire shape matches the middleware's `{ "error": "<code>" }` format by inspecting the response body in the test.

- [ ] **Step 4: Run tests — expect green**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter RegisterEndpointTests`
Expected: all 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Api/Endpoints/Agents/RegisterEndpoint.cs tests/Sigil.Api.Tests/Endpoints/Agents/RegisterEndpointTests.cs
git commit -m "feat(api): POST /api/agents/register"
```

---

## Task 13: `POST /api/agents/{id}/deregister`

**Files:**
- Create: `src/Sigil.Api/Endpoints/Agents/DeregisterEndpoint.cs`
- Create: `tests/Sigil.Api.Tests/Endpoints/Agents/DeregisterEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class DeregisterEndpointTests
{
    private static AgentRegistration NewHealthy(string id) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = AgentStatus.Healthy,
        Model = new ModelSpec { Provider = "test", Name = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo" } },
    };

    [Fact]
    public async Task HappyPath_Returns204()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentA));
        await factory.Store.UpdateStatusAsync(new AgentId(TestKeys.AgentA), AgentStatus.Healthy);

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownAgent_Returns404()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"agent-not-found\"");
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentB}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"caller-agent-mismatch\"");
    }
}
```

- [ ] **Step 2: Run tests — expect 404 because endpoint doesn't exist**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter DeregisterEndpointTests`
Expected: all 3 fail with 404.

- [ ] **Step 3: Write the endpoint**

```csharp
using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed record DeregisterRequest
{
    public string Id { get; init; } = "";
}

public sealed class DeregisterEndpoint : Endpoint<DeregisterRequest>
{
    private readonly IAgentRegistry _registry;
    public DeregisterEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Post("/api/agents/{id}/deregister");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeregisterRequest req, CancellationToken ct)
    {
        var caller = HttpContext.GetAuthenticatedAgentId();
        var target = new AgentId(req.Id);
        if (caller != target)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "caller-agent-mismatch" }, ct);
            return;
        }

        var result = await _registry.MarkOfflineAsync(target, ct);
        if (result.IsFailure)
        {
            var status = result.Error switch
            {
                RegistryErrors.AgentNotFound => StatusCodes.Status404NotFound,
                RegistryErrors.InvalidStatusTransition => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            HttpContext.Response.StatusCode = status;
            await HttpContext.Response.WriteAsJsonAsync(new { error = result.Error }, ct);
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter DeregisterEndpointTests`
Expected: 3 pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Api/Endpoints/Agents/DeregisterEndpoint.cs tests/Sigil.Api.Tests/Endpoints/Agents/DeregisterEndpointTests.cs
git commit -m "feat(api): POST /api/agents/{id}/deregister"
```

---

## Task 14: `POST /api/agents/{id}/heartbeat`

**Files:**
- Create: `src/Sigil.Api/Endpoints/Agents/HeartbeatEndpoint.cs`
- Create: `tests/Sigil.Api.Tests/Endpoints/Agents/HeartbeatEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class HeartbeatEndpointTests
{
    private static AgentRegistration NewAgent(string id, AgentStatus status) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = status,
        Model = new ModelSpec { Provider = "test", Name = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo" } },
    };

    [Fact]
    public async Task HappyPath_Returns204()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, AgentStatus.Healthy));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownAgent_Returns404()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"agent-not-found\"");
    }

    [Fact]
    public async Task OfflineAgent_Returns409()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, AgentStatus.Offline));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"invalid-status-transition\"");
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentB}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 2: Write the endpoint**

```csharp
using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed record HeartbeatRequest
{
    public string Id { get; init; } = "";
}

public sealed class HeartbeatEndpoint : Endpoint<HeartbeatRequest>
{
    private readonly IAgentRegistry _registry;
    public HeartbeatEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Post("/api/agents/{id}/heartbeat");
        AllowAnonymous();
    }

    public override async Task HandleAsync(HeartbeatRequest req, CancellationToken ct)
    {
        var caller = HttpContext.GetAuthenticatedAgentId();
        var target = new AgentId(req.Id);
        if (caller != target)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "caller-agent-mismatch" }, ct);
            return;
        }

        var result = await _registry.HeartbeatAsync(target, ct);
        if (result.IsFailure)
        {
            var status = result.Error switch
            {
                RegistryErrors.AgentNotFound => StatusCodes.Status404NotFound,
                RegistryErrors.InvalidStatusTransition => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            HttpContext.Response.StatusCode = status;
            await HttpContext.Response.WriteAsJsonAsync(new { error = result.Error }, ct);
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter HeartbeatEndpointTests`
Expected: 4 pass.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Api/Endpoints/Agents/HeartbeatEndpoint.cs tests/Sigil.Api.Tests/Endpoints/Agents/HeartbeatEndpointTests.cs
git commit -m "feat(api): POST /api/agents/{id}/heartbeat"
```

---

## Task 15: `GET /api/agents`

**Files:**
- Create: `src/Sigil.Api/Endpoints/Agents/ListAgentsEndpoint.cs`
- Create: `tests/Sigil.Api.Tests/Endpoints/Agents/ListAgentsEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class ListAgentsEndpointTests
{
    private static AgentRegistration NewAgent(string id, string domain, string skill) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = domain,
        EndpointUrl = "https://localhost:9000",
        Model = new ModelSpec { Provider = "test", Name = "test" },
        Skills = new[] { new Skill { Name = skill, Description = skill } },
    };

    [Fact]
    public async Task Empty_ReturnsEmptyArray()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AgentRegistration[]>();
        body.ShouldNotBeNull();
        body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Populated_ReturnsAll()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "test", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "test", "reverse"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents");

        body!.Length.ShouldBe(2);
    }

    [Fact]
    public async Task FilterBySkill_ReturnsMatching()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "test", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "test", "reverse"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents?skill=echo");

        body!.Length.ShouldBe(1);
        body[0].AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task FilterByDomain_ReturnsMatching()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "alpha", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "beta", "echo"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents?domain=alpha");

        body!.Length.ShouldBe(1);
        body[0].AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task BothFilters_Returns400()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.GetAsync("/api/agents?skill=echo&domain=alpha");

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"conflicting-filters\"");
    }
}
```

- [ ] **Step 2: Write the endpoint**

```csharp
using FastEndpoints;
using Sigil.Core.Registry;

namespace Sigil.Api.Endpoints.Agents;

public sealed record ListAgentsRequest
{
    public string? Skill { get; init; }
    public string? Domain { get; init; }
}

public sealed class ListAgentsEndpoint : Endpoint<ListAgentsRequest, IReadOnlyList<AgentRegistration>>
{
    private readonly IAgentRegistry _registry;
    public ListAgentsEndpoint(IAgentRegistry registry) => _registry = registry;

    public override void Configure()
    {
        Get("/api/agents");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListAgentsRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.Skill) && !string.IsNullOrWhiteSpace(req.Domain))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "conflicting-filters" }, ct);
            return;
        }

        IReadOnlyList<AgentRegistration> result =
            !string.IsNullOrWhiteSpace(req.Skill) ? await _registry.FindBySkillAsync(req.Skill, ct)
            : !string.IsNullOrWhiteSpace(req.Domain) ? await _registry.FindByDomainAsync(req.Domain, ct)
            : await _registry.GetAllAsync(ct);

        await SendAsync(result, StatusCodes.Status200OK, ct);
    }
}
```

- [ ] **Step 3: Run tests (including the previously-failing middleware test)**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter "ListAgentsEndpointTests|SigilAuthMiddlewareTests"`
Expected: all 5 list tests pass, and `ValidHeaders_ReachesEndpoint` from Task 11 now passes too.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Api/Endpoints/Agents/ListAgentsEndpoint.cs tests/Sigil.Api.Tests/Endpoints/Agents/ListAgentsEndpointTests.cs
git commit -m "feat(api): GET /api/agents (with skill/domain filters)"
```

---

## Task 16: `POST /api/intents`

**Files:**
- Create: `src/Sigil.Api/Endpoints/Intents/SubmitIntentEndpoint.cs`
- Create: `tests/Sigil.Api.Tests/Endpoints/Intents/SubmitIntentEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;

namespace Sigil.Api.Tests.Endpoints.Intents;

public sealed class SubmitIntentEndpointTests
{
    private sealed record IntentDto(string SkillName, string Input);

    private static AgentRegistration NewHealthy(string id, string skill) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = AgentStatus.Healthy,
        Model = new ModelSpec { Provider = "test", Name = "test" },
        Skills = new[] { new Skill { Name = skill, Description = skill } },
    };

    [Fact]
    public async Task NoAgentForSkill_Returns404()
    {
        var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"no-agent-for-skill\"");
    }

    [Fact]
    public async Task HappyPath_Returns200WithExecutionResult()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AgentExecutionResult>();
        body.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidationRejected_Returns400()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        factory.Gateway.OnValidate = (_, _) =>
            Result.Success(new ValidationResult { CanHandle = false, Reason = "tokens-exceeded" });

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"tokens-exceeded\"");
    }

    [Fact]
    public async Task GatewayFails_Returns502()
    {
        var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        factory.Gateway.OnValidate = (_, _) => Result.Failure<ValidationResult>("circuit-open");

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"circuit-open\"");
    }
}
```

- [ ] **Step 2: Write the endpoint**

```csharp
using FastEndpoints;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;

namespace Sigil.Api.Endpoints.Intents;

public sealed record SubmitIntentRequest
{
    public required string SkillName { get; init; }
    public required string Input { get; init; }
    public ContextSnapshot? Snapshot { get; init; }
}

public sealed class SubmitIntentEndpoint : Endpoint<SubmitIntentRequest, AgentExecutionResult>
{
    private readonly IIntentDispatcher _dispatcher;
    public SubmitIntentEndpoint(IIntentDispatcher dispatcher) => _dispatcher = dispatcher;

    public override void Configure()
    {
        Post("/api/intents");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SubmitIntentRequest req, CancellationToken ct)
    {
        var result = await _dispatcher.DispatchAsync(new IntentRequest
        {
            SkillName = req.SkillName,
            Input = req.Input,
            Snapshot = req.Snapshot,
        }, ct);

        if (result.IsSuccess)
        {
            await SendAsync(result.Value, StatusCodes.Status200OK, ct);
            return;
        }

        var status = result.Error switch
        {
            IntentErrors.NoAgentForSkill => StatusCodes.Status404NotFound,
            IntentErrors.ValidationRejected => StatusCodes.Status400BadRequest,
            // Known validation-side rejection reasons surfaced by the agent come back as the
            // raw reason string; we treat them all as 400 (the agent said "no, here's why").
            // Anything else is treated as a gateway failure (transport/circuit/upstream 5xx).
            _ when IsValidationReason(result.Error) => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status502BadGateway,
        };

        HttpContext.Response.StatusCode = status;
        await HttpContext.Response.WriteAsJsonAsync(new { error = result.Error }, ct);
    }

    // Heuristic: validation-side rejections are produced by SimpleIntentDispatcher when
    // ValidationResult.CanHandle is false. Those errors are NOT in any well-known gateway
    // error set, so we distinguish them by checking if the gateway also reported the
    // failure (currently impossible to know server-side without richer typing). For Phase 1,
    // we treat the SigilGatewayErrors.* set as 502 and everything else as 400.
    private static bool IsValidationReason(string error) =>
        !SigilGatewayErrorSet.All.Contains(error)
        && error != IntentErrors.NoAgentForSkill;
}
```

You will also need a small lookup of gateway error codes. Inspect `src/Sigil.Core/Gateway/SigilGatewayErrors.cs` to confirm the constants. If `SigilGatewayErrors` exposes a `Set` or similar already, use it; otherwise add this helper to `Sigil.Api/Endpoints/Intents/SigilGatewayErrorSet.cs`:

```csharp
using Sigil.Core.Gateway;

namespace Sigil.Api.Endpoints.Intents;

internal static class SigilGatewayErrorSet
{
    // Populate from the constants in Sigil.Core.Gateway.SigilGatewayErrors —
    // verify field names with a quick read; this is the expected shape based on issue #10.
    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        SigilGatewayErrors.Timeout,
        SigilGatewayErrors.CircuitOpen,
        SigilGatewayErrors.Transport,
        SigilGatewayErrors.Upstream5xx,
        SigilGatewayErrors.Upstream4xx,
        SigilGatewayErrors.SignatureInvalid,
    };
}
```

If any of these constants don't exist with that exact name, replace with the actual names from `SigilGatewayErrors.cs`. The test `GatewayFails_Returns502` uses `"circuit-open"` as the literal — adjust both the test and the set to whatever the real constant value is (the test currently assumes the kebab-case form).

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj --filter SubmitIntentEndpointTests`
Expected: all 4 pass.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Api/Endpoints/Intents/ tests/Sigil.Api.Tests/Endpoints/Intents/
git commit -m "feat(api): POST /api/intents (Phase 1 synchronous dispatch)"
```

---

## Task 17: Sample dev configuration

**Files:**
- Modify: `src/Sigil.Api/appsettings.Development.json`

- [ ] **Step 1: Inspect the current contents**

Run: `cat src/Sigil.Api/appsettings.Development.json`
Note the current keys. The file is on the PreToolUse deny-list per `CLAUDE.md` — but **only when *.Development.json is edited via Edit/Write tools**. Verify the hook config does not block this file: `cat .claude/settings.json | head -40`. If blocked, request user-side edit via `! cat > ... <<EOF`.

If allowed, merge in:

```json
{
  "Security": {
    "Mode": "Open",
    "OpenTier": {
      "Keys": {
        "echo-agent-local": "replace-me-local-dev-key"
      }
    }
  },
  "Storage": {
    "ConnectionString": "Host=localhost;Database=sigil;Username=sigil;Password=sigil"
  }
}
```

Preserve any pre-existing keys (e.g. `Logging`). This is a *sample* — actual keys for compose come from environment variables (already supported by `SigilSecurityOptions` via configuration binding).

- [ ] **Step 2: Commit**

```bash
git add src/Sigil.Api/appsettings.Development.json
git commit -m "chore(api): sample Security + Storage config for local dev"
```

---

## Task 18: Full build + test gate

**Files:** none (verification only)

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build sigil.sln`
Expected: zero warnings/errors.

- [ ] **Step 2: Test the whole solution**

Run: `dotnet test sigil.sln`
Expected: all tests pass, including:
- the 5 new `SimpleIntentDispatcherTests`
- the 5 `SigilAuthMiddlewareTests`
- the 4 `RegisterEndpointTests`
- the 3 `DeregisterEndpointTests`
- the 4 `HeartbeatEndpointTests`
- the 5 `ListAgentsEndpointTests`
- the 4 `SubmitIntentEndpointTests`
- all existing tests still green

- [ ] **Step 3: If anything is red, fix and re-run before moving on**

Common failure modes:
- `SigilGatewayErrors.*` constant names differ from what Task 16 assumed → update the set.
- `ContextDelta` requires more `required` properties than the no-arg constructor provides → update `StubAgentGateway` and `FakeAgentGateway` defaults.
- `appsettings.Development.json` blocked by the PreToolUse hook → ask the user to apply manually.

---

## Task 19: Open the PR

**Files:** none (git/gh only)

- [ ] **Step 1: Push the branch**

Run: `git push -u origin feature/issue-13-fastendpoints-lifecycle-intent`

- [ ] **Step 2: Open the PR**

Run:
```bash
gh pr create --title "feat(api): FastEndpoints agent lifecycle + intent (closes #13)" --body "$(cat <<'EOF'
## Summary
- Adds `SigilAuthMiddleware` gating all API traffic with `X-Sigil-Agent-Id` + `X-Sigil-Key` against the Open tier.
- Implements `POST /api/agents/register`, `POST /api/agents/{id}/deregister`, `POST /api/agents/{id}/heartbeat`, `GET /api/agents`, and `POST /api/intents`.
- Introduces `IIntentDispatcher` in `Sigil.Core` and `SimpleIntentDispatcher` in `Sigil.Runtime` as a Phase-1 seam (registry select → gateway validate → execute). Phase 2's planner-driven orchestrator will swap the implementation without touching the endpoint.

## Test plan
- [x] `dotnet build sigil.sln`
- [x] `dotnet test sigil.sln` (new: Sigil.Api.Tests + SimpleIntentDispatcherTests; all existing green)
- [ ] Manual smoke after merge: `docker compose up`, register a stub agent, submit an intent.

Closes #13.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Report the PR URL**

The terminal will print the PR URL. Share it back.

---

## Self-Review notes

- **Spec coverage** — every deliverable from issue #13 maps to a task: `POST /register` (Task 12), `POST /{id}/deregister` (Task 13), `POST /{id}/heartbeat` (Task 14), `GET /agents` (Task 15), `POST /intents` (Task 16), DTOs under `Endpoints/*` (each endpoint task), auth gate covering all five (Tasks 9 + 11).
- **Placeholders** — none. Each task has runnable code and concrete commands.
- **Type consistency** — `IIntentDispatcher.DispatchAsync` signature is identical in interface (Task 3), implementation (Task 6), and endpoint use (Task 16). Error code constants (`IntentErrors.NoAgentForSkill`, `RegistryErrors.AgentNotFound`, `SigilSecurityErrors.*`) are referenced by their canonical names everywhere.
- **Known fragile spots** flagged inline: `SigilGatewayErrors` constant names (Task 16 step 2), `ContextDelta` constructor (Tasks 4 + 10), and the `appsettings.Development.json` PreToolUse hook (Task 17).
