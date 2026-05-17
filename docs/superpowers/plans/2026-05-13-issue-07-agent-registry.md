# Issue #7 — Agent Registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land `IAgentRegistry` (Core) and `AgentRegistry` (Runtime) — a façade over `IAgentRegistrationStore` that enforces `AgentStatus` transitions and performs weighted random selection over Healthy agents for a given skill.

**Architecture:** Registry wraps the existing `IAgentRegistrationStore`. Transition rules live in the Runtime implementation; the store stays a dumb persistence boundary. A tiny `IRandomProvider` abstraction makes weighted-selection tests deterministic. A new `tests/Sigil.Runtime.Tests` project hosts behavioral tests with an in-memory fake store.

**Tech Stack:** .NET 9, C# 13, xUnit 2.9, Shouldly 4.3, CSharpFunctionalExtensions 3.7 (`Result`, `Maybe<T>`). Central Package Management — `PackageReference` entries carry no `Version` attribute.

**Spec:** `docs/superpowers/specs/2026-05-13-issue-07-agent-registry-design.md`

---

## File map

**Create (`Sigil.Core`)**
- `src/Sigil.Core/Registry/IAgentRegistry.cs` — interface
- `src/Sigil.Core/Registry/IRandomProvider.cs` — tiny random abstraction
- `src/Sigil.Core/Registry/RegistryErrors.cs` — string error code constants

**Create (`Sigil.Runtime`)**
- `src/Sigil.Runtime/Registry/AgentRegistry.cs` — implementation
- `src/Sigil.Runtime/Registry/SystemRandomProvider.cs` — `Random.Shared` adapter
- `src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs` — `AddSigilRuntime()`

**Modify**
- `src/Sigil.Runtime/Sigil.Runtime.csproj` — add `Sigil.Core` project reference and `Microsoft.Extensions.DependencyInjection.Abstractions` package reference

**Create (tests)**
- `tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
- `tests/Sigil.Runtime.Tests/Registry/FakeAgentRegistrationStore.cs`
- `tests/Sigil.Runtime.Tests/Registry/StubRandomProvider.cs`
- `tests/Sigil.Runtime.Tests/Registry/AgentRegistryRegistrationTests.cs`
- `tests/Sigil.Runtime.Tests/Registry/AgentRegistryHeartbeatTests.cs`
- `tests/Sigil.Runtime.Tests/Registry/AgentRegistryTransitionTests.cs`
- `tests/Sigil.Runtime.Tests/Registry/AgentRegistryWeightedSelectionTests.cs`
- `tests/Sigil.Runtime.Tests/Registry/TestAgents.cs` — shared factory helpers
- `tests/Sigil.Core.Tests/Registry/RegistryErrorsTests.cs`

**Modify**
- `sigil.sln` — register `Sigil.Runtime.Tests`

---

## Conventions reminders

- `Directory.Build.props` enables nullable + warnings-as-errors. Every public type has explicit nullability and XML-free code (no XML docs unless project warns).
- Central Package Management is on. **Never** add a `Version="…"` to `<PackageReference>`. If a new package is needed, also add a `<PackageVersion>` entry to `Directory.Packages.props`.
- All result types use `CSharpFunctionalExtensions.Result` (no error-as-exception). Errors are stable lowercase-hyphen strings.
- `AgentId` is `readonly record struct AgentId(string Value)`. Compare with `==`; render with `.Value` or `.ToString()`.
- Test files use Shouldly + xUnit (`[Fact]`, `[Theory]`, `Should*`).
- One commit per task. Commit body trailer: `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.

---

## Task 1 — Add `IRandomProvider` to `Sigil.Core`

**Files**
- Create: `src/Sigil.Core/Registry/IRandomProvider.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Sigil.Core.Registry;

/// <summary>
/// Pluggable random source so weighted-selection tests can be deterministic.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a non-negative random integer less than <paramref name="maxExclusive"/>.
    /// </summary>
    /// <param name="maxExclusive">Exclusive upper bound. Must be &gt; 0.</param>
    int Next(int maxExclusive);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Core/Sigil.Core.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Registry/IRandomProvider.cs
git commit -m "feat(core): add IRandomProvider abstraction for deterministic selection tests"
```

---

## Task 2 — Add `RegistryErrors` constants

**Files**
- Create: `src/Sigil.Core/Registry/RegistryErrors.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace Sigil.Core.Registry;

/// <summary>
/// Stable string error codes returned by <see cref="IAgentRegistry"/>.
/// Consumers (endpoints, logs, tests) match on these values.
/// </summary>
public static class RegistryErrors
{
    public const string AgentNotFound = "agent-not-found";
    public const string InvalidStatusTransition = "invalid-status-transition";
    public const string InvalidRoutingWeight = "invalid-routing-weight";
    public const string SkillNameRequired = "skill-name-required";
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Core/Sigil.Core.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Registry/RegistryErrors.cs
git commit -m "feat(core): add RegistryErrors string constants"
```

---

## Task 3 — Pin error-code constants with a guard test

**Files**
- Create: `tests/Sigil.Core.Tests/Registry/RegistryErrorsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class RegistryErrorsTests
{
    [Fact]
    public void Constants_have_stable_string_values()
    {
        RegistryErrors.AgentNotFound.ShouldBe("agent-not-found");
        RegistryErrors.InvalidStatusTransition.ShouldBe("invalid-status-transition");
        RegistryErrors.InvalidRoutingWeight.ShouldBe("invalid-routing-weight");
        RegistryErrors.SkillNameRequired.ShouldBe("skill-name-required");
    }
}
```

- [ ] **Step 2: Run test**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter FullyQualifiedName~RegistryErrorsTests`
Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Core.Tests/Registry/RegistryErrorsTests.cs
git commit -m "test(core): pin RegistryErrors string values"
```

---

## Task 4 — Add `IAgentRegistry` interface to `Sigil.Core`

**Files**
- Create: `src/Sigil.Core/Registry/IAgentRegistry.cs`

- [ ] **Step 1: Write the file**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

/// <summary>
/// Kernel-side façade over <c>IAgentRegistrationStore</c>. Owns <see cref="AgentStatus"/>
/// lifecycle rules and weighted selection for canary-style routing.
/// </summary>
public interface IAgentRegistry
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> GetAsync(AgentId id, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>
    /// Refresh the agent's heartbeat. Promotes Starting/Degraded → Healthy; rejects Offline.
    /// </summary>
    Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default);

    Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default);

    Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default);

    /// <summary>
    /// Pick one Healthy agent advertising <paramref name="skillName"/>, weighted by RoutingWeight.
    /// Returns <see cref="Maybe.None"/> when no eligible candidate exists.
    /// </summary>
    Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Core/Sigil.Core.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Registry/IAgentRegistry.cs
git commit -m "feat(core): add IAgentRegistry interface"
```

---

## Task 5 — Wire `Sigil.Runtime` project references

**Files**
- Modify: `src/Sigil.Runtime/Sigil.Runtime.csproj`
- Modify: `Directory.Packages.props` (verify `Microsoft.Extensions.DependencyInjection.Abstractions` exists — it does at v10.0.5)

- [ ] **Step 1: Inspect current state**

Run: `cat src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: Currently has no `<ItemGroup>` (bare SDK project).

- [ ] **Step 2: Replace contents**

Write `src/Sigil.Runtime/Sigil.Runtime.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Sigil.Core\Sigil.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Runtime/Sigil.Runtime.csproj
git commit -m "build(runtime): reference Sigil.Core and DI abstractions"
```

---

## Task 6 — `SystemRandomProvider` adapter

**Files**
- Create: `src/Sigil.Runtime/Registry/SystemRandomProvider.cs`

- [ ] **Step 1: Write the file**

```csharp
using Sigil.Core.Registry;

namespace Sigil.Runtime.Registry;

/// <summary>
/// Default <see cref="IRandomProvider"/> backed by <see cref="System.Random.Shared"/>.
/// </summary>
public sealed class SystemRandomProvider : IRandomProvider
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Runtime/Registry/SystemRandomProvider.cs
git commit -m "feat(runtime): add SystemRandomProvider"
```

---

## Task 7 — Skeleton `AgentRegistry` (compiles, throws everywhere)

We split the implementation into incremental, test-driven slices. This task lands the skeleton so subsequent TDD tasks have a class to target.

**Files**
- Create: `src/Sigil.Runtime/Registry/AgentRegistry.cs`

- [ ] **Step 1: Write the file**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Storage;

namespace Sigil.Runtime.Registry;

/// <summary>
/// Default <see cref="IAgentRegistry"/> implementation. Wraps <see cref="IAgentRegistrationStore"/>
/// and enforces status-transition rules and weighted selection.
/// </summary>
public sealed class AgentRegistry : IAgentRegistry
{
    private readonly IAgentRegistrationStore _store;
    private readonly IRandomProvider _random;

    public AgentRegistry(IAgentRegistrationStore store, IRandomProvider random)
    {
        _store = store;
        _random = random;
    }

    public Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Maybe<AgentRegistration>> GetAsync(AgentId id, CancellationToken ct = default)
        => _store.GetAsync(id, ct);

    public Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default)
        => _store.GetAllAsync(ct);

    public Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
        => _store.FindBySkillAsync(skillName, ct);

    public Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default)
        => _store.FindByDomainAsync(domain, ct);

    public Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Runtime/Registry/AgentRegistry.cs
git commit -m "feat(runtime): AgentRegistry skeleton"
```

---

## Task 8 — Add `Sigil.Runtime.Tests` project and register in solution

**Files**
- Create: `tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
- Modify: `sigil.sln`

- [ ] **Step 1: Create test csproj**

Write `tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`:

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
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Sigil.Core\Sigil.Core.csproj" />
    <ProjectReference Include="..\..\src\Sigil.Runtime\Sigil.Runtime.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

Run: `dotnet sln sigil.sln add tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --solution-folder tests`
Expected: `Project ... added to the solution.`

- [ ] **Step 3: Verify solution build**

Run: `dotnet build sigil.sln`
Expected: Build succeeded, 0 warnings. `Sigil.Runtime.Tests` appears in the build output.

- [ ] **Step 4: Commit**

```bash
git add tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj sigil.sln
git commit -m "build(test): add Sigil.Runtime.Tests project"
```

---

## Task 9 — Shared test fixtures (`FakeAgentRegistrationStore`, `StubRandomProvider`, `TestAgents`)

**Files**
- Create: `tests/Sigil.Runtime.Tests/Registry/FakeAgentRegistrationStore.cs`
- Create: `tests/Sigil.Runtime.Tests/Registry/StubRandomProvider.cs`
- Create: `tests/Sigil.Runtime.Tests/Registry/TestAgents.cs`

- [ ] **Step 1: Write `FakeAgentRegistrationStore`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Core.Storage;

namespace Sigil.Runtime.Tests.Registry;

/// <summary>
/// Minimal in-memory <see cref="IAgentRegistrationStore"/> for unit tests.
/// No concurrency control; tests are single-threaded.
/// </summary>
internal sealed class FakeAgentRegistrationStore : IAgentRegistrationStore
{
    private readonly Dictionary<AgentId, AgentRegistration> _items = new();

    public IReadOnlyDictionary<AgentId, AgentRegistration> Snapshot => _items;

    public Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
    {
        _items[registration.AgentId] = registration;
        return Task.FromResult(Result.Success());
    }

    public Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(agentId, out var v) ? Maybe.From(v) : Maybe<AgentRegistration>.None);

    public Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(_items.Values.ToList());

    public Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(
            _items.Values.Where(a => a.Skills.Any(s => s.Name == skillName)).ToList());

    public Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentRegistration>>(
            _items.Values.Where(a => a.Domain == domain).ToList());

    public Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(agentId, out var existing))
            return Task.FromResult(Result.Failure(RegistryErrors.AgentNotFound));
        _items[agentId] = existing with { LastHeartbeat = DateTime.UtcNow };
        return Task.FromResult(Result.Success());
    }

    public Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(agentId, out var existing))
            return Task.FromResult(Result.Failure(RegistryErrors.AgentNotFound));
        _items[agentId] = existing with { Status = status };
        return Task.FromResult(Result.Success());
    }
}
```

- [ ] **Step 2: Write `StubRandomProvider`**

```csharp
using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Registry;

/// <summary>
/// Deterministic random provider for tests. Either replays a queued sequence of values
/// or delegates to a seeded <see cref="System.Random"/>.
/// </summary>
internal sealed class StubRandomProvider : IRandomProvider
{
    private readonly Queue<int>? _queue;
    private readonly Random? _seeded;

    public StubRandomProvider(IEnumerable<int> values) => _queue = new Queue<int>(values);

    public StubRandomProvider(int seed) => _seeded = new Random(seed);

    public int Next(int maxExclusive)
    {
        if (_queue is not null)
        {
            var raw = _queue.Dequeue();
            return ((raw % maxExclusive) + maxExclusive) % maxExclusive;
        }
        return _seeded!.Next(maxExclusive);
    }
}
```

- [ ] **Step 3: Write `TestAgents` factory**

```csharp
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Registry;

internal static class TestAgents
{
    public static AgentRegistration Make(
        string id,
        AgentStatus status = AgentStatus.Starting,
        int routingWeight = 100,
        string skillName = "echo")
        => new()
        {
            AgentId = new AgentId(id),
            Name = id,
            Domain = "test",
            EndpointUrl = $"https://{id}.internal",
            RoutingWeight = routingWeight,
            Status = status,
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" },
            Skills =
            [
                new Skill { Name = skillName, Description = "test skill" }
            ]
        };
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add tests/Sigil.Runtime.Tests/Registry/
git commit -m "test(runtime): add fakes and factories for registry tests"
```

---

## Task 10 — Implement `RegisterAsync` (TDD)

**Files**
- Create: `tests/Sigil.Runtime.Tests/Registry/AgentRegistryRegistrationTests.cs`
- Modify: `src/Sigil.Runtime/Registry/AgentRegistry.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryRegistrationTests
{
    private static AgentRegistry NewRegistry(out FakeAgentRegistrationStore store)
    {
        store = new FakeAgentRegistrationStore();
        return new AgentRegistry(store, new StubRandomProvider(seed: 1));
    }

    [Fact]
    public async Task Register_persists_agent_with_status_Starting()
    {
        var registry = NewRegistry(out var store);
        var agent = TestAgents.Make("alpha", status: AgentStatus.Healthy /* should be overridden */);

        var result = await registry.RegisterAsync(agent);

        result.IsSuccess.ShouldBeTrue();
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(AgentStatus.Starting);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Register_rejects_invalid_routing_weight(int weight)
    {
        var registry = NewRegistry(out var store);
        var agent = TestAgents.Make("alpha", routingWeight: weight);

        var result = await registry.RegisterAsync(agent);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.InvalidRoutingWeight);
        store.Snapshot.ShouldBeEmpty();
    }

}
```

- [ ] **Step 2: Run tests — expect failure**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryRegistrationTests`
Expected: Tests fail with `NotImplementedException`.

- [ ] **Step 3: Implement `RegisterAsync`**

In `src/Sigil.Runtime/Registry/AgentRegistry.cs`, replace `RegisterAsync`:

```csharp
public Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
{
    if (registration.RoutingWeight < 0 || registration.RoutingWeight > 100)
        return Task.FromResult(Result.Failure(RegistryErrors.InvalidRoutingWeight));

    var normalized = registration with { Status = AgentStatus.Starting };
    return _store.RegisterAsync(normalized, ct);
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryRegistrationTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Runtime/Registry/AgentRegistry.cs tests/Sigil.Runtime.Tests/Registry/AgentRegistryRegistrationTests.cs
git commit -m "feat(runtime): AgentRegistry.RegisterAsync — normalizes to Starting, validates weight"
```

---

## Task 11 — Implement `HeartbeatAsync` (TDD)

**Files**
- Create: `tests/Sigil.Runtime.Tests/Registry/AgentRegistryHeartbeatTests.cs`
- Modify: `src/Sigil.Runtime/Registry/AgentRegistry.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryHeartbeatTests
{
    private static (AgentRegistry registry, FakeAgentRegistrationStore store) Make()
    {
        var store = new FakeAgentRegistrationStore();
        return (new AgentRegistry(store, new StubRandomProvider(seed: 1)), store);
    }

    [Theory]
    [InlineData(AgentStatus.Starting,  AgentStatus.Healthy)]
    [InlineData(AgentStatus.Healthy,   AgentStatus.Healthy)]
    [InlineData(AgentStatus.Degraded,  AgentStatus.Healthy)]
    [InlineData(AgentStatus.Draining,  AgentStatus.Draining)]
    public async Task Heartbeat_promotes_or_preserves_status(AgentStatus from, AgentStatus expected)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));
        var before = store.Snapshot[new AgentId("alpha")].LastHeartbeat;
        await Task.Delay(5);

        var result = await registry.HeartbeatAsync(new AgentId("alpha"));

        result.IsSuccess.ShouldBeTrue();
        var after = store.Snapshot[new AgentId("alpha")];
        after.Status.ShouldBe(expected);
        after.LastHeartbeat.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Heartbeat_rejects_offline_agent()
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: AgentStatus.Offline));

        var result = await registry.HeartbeatAsync(new AgentId("alpha"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.InvalidStatusTransition);
    }

    [Fact]
    public async Task Heartbeat_returns_agent_not_found_for_unknown_id()
    {
        var (registry, _) = Make();

        var result = await registry.HeartbeatAsync(new AgentId("ghost"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.AgentNotFound);
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryHeartbeatTests`
Expected: `NotImplementedException`.

- [ ] **Step 3: Implement `HeartbeatAsync`**

Replace `HeartbeatAsync` in `src/Sigil.Runtime/Registry/AgentRegistry.cs`:

```csharp
public async Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default)
{
    var maybe = await _store.GetAsync(id, ct);
    if (maybe.HasNoValue)
        return Result.Failure(RegistryErrors.AgentNotFound);

    var current = maybe.Value.Status;
    if (current == AgentStatus.Offline)
        return Result.Failure(RegistryErrors.InvalidStatusTransition);

    var beat = await _store.UpdateHeartbeatAsync(id, ct);
    if (beat.IsFailure)
        return beat;

    if (current is AgentStatus.Starting or AgentStatus.Degraded)
        return await _store.UpdateStatusAsync(id, AgentStatus.Healthy, ct);

    return Result.Success();
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryHeartbeatTests`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Runtime/Registry/AgentRegistry.cs tests/Sigil.Runtime.Tests/Registry/AgentRegistryHeartbeatTests.cs
git commit -m "feat(runtime): AgentRegistry.HeartbeatAsync — promote Starting/Degraded, reject Offline"
```

---

## Task 12 — Implement explicit status transitions (TDD)

Implements `MarkHealthyAsync`, `MarkDegradedAsync`, `MarkOfflineAsync`, `BeginDrainingAsync` and pins the matrix from spec §5.

Transition matrix:

| From \ To  | Healthy | Degraded | Offline | Draining |
|------------|---------|----------|---------|----------|
| Starting   |   ✅    |    —     |   ✅    |    —     |
| Healthy    |   —     |   ✅     |   ✅    |   ✅     |
| Degraded   |   ✅    |    —     |   ✅    |   ✅     |
| Offline    |   —     |   —      |   —     |   —      |
| Draining   |   —     |   —      |   ✅    |   —      |

**Files**
- Create: `tests/Sigil.Runtime.Tests/Registry/AgentRegistryTransitionTests.cs`
- Modify: `src/Sigil.Runtime/Registry/AgentRegistry.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryTransitionTests
{
    private static (AgentRegistry registry, FakeAgentRegistrationStore store) Make()
    {
        var store = new FakeAgentRegistrationStore();
        return (new AgentRegistry(store, new StubRandomProvider(seed: 1)), store);
    }

    public static IEnumerable<object[]> LegalTransitions => new[]
    {
        new object[] { AgentStatus.Starting,  AgentStatus.Healthy },
        new object[] { AgentStatus.Starting,  AgentStatus.Offline },
        new object[] { AgentStatus.Healthy,   AgentStatus.Degraded },
        new object[] { AgentStatus.Healthy,   AgentStatus.Offline },
        new object[] { AgentStatus.Healthy,   AgentStatus.Draining },
        new object[] { AgentStatus.Degraded,  AgentStatus.Healthy },
        new object[] { AgentStatus.Degraded,  AgentStatus.Offline },
        new object[] { AgentStatus.Degraded,  AgentStatus.Draining },
        new object[] { AgentStatus.Draining,  AgentStatus.Offline },
    };

    public static IEnumerable<object[]> IllegalTransitions => new[]
    {
        new object[] { AgentStatus.Starting,  AgentStatus.Degraded },
        new object[] { AgentStatus.Starting,  AgentStatus.Draining },
        new object[] { AgentStatus.Healthy,   AgentStatus.Healthy },
        new object[] { AgentStatus.Degraded,  AgentStatus.Degraded },
        new object[] { AgentStatus.Offline,   AgentStatus.Healthy },
        new object[] { AgentStatus.Offline,   AgentStatus.Degraded },
        new object[] { AgentStatus.Offline,   AgentStatus.Draining },
        new object[] { AgentStatus.Draining,  AgentStatus.Healthy },
        new object[] { AgentStatus.Draining,  AgentStatus.Degraded },
        new object[] { AgentStatus.Draining,  AgentStatus.Draining },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task Legal_transition_succeeds(AgentStatus from, AgentStatus to)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));

        var result = await InvokeTransition(registry, new AgentId("alpha"), to);

        result.IsSuccess.ShouldBeTrue($"{from} → {to} should be legal");
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(to);
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public async Task Illegal_transition_is_rejected(AgentStatus from, AgentStatus to)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));

        var result = await InvokeTransition(registry, new AgentId("alpha"), to);

        result.IsFailure.ShouldBeTrue($"{from} → {to} should be illegal");
        result.Error.ShouldBe(RegistryErrors.InvalidStatusTransition);
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(from);
    }

    [Fact]
    public async Task Transition_returns_agent_not_found_for_unknown_id()
    {
        var (registry, _) = Make();

        var result = await registry.MarkHealthyAsync(new AgentId("ghost"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.AgentNotFound);
    }

    private static Task<CSharpFunctionalExtensions.Result> InvokeTransition(
        AgentRegistry registry, AgentId id, AgentStatus to) => to switch
    {
        AgentStatus.Healthy   => registry.MarkHealthyAsync(id),
        AgentStatus.Degraded  => registry.MarkDegradedAsync(id),
        AgentStatus.Offline   => registry.MarkOfflineAsync(id),
        AgentStatus.Draining  => registry.BeginDrainingAsync(id),
        _ => throw new ArgumentOutOfRangeException(nameof(to), to, "Test does not cover this target.")
    };
}
```

- [ ] **Step 2: Run tests — expect failure**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryTransitionTests`
Expected: `NotImplementedException`.

- [ ] **Step 3: Implement transitions**

In `src/Sigil.Runtime/Registry/AgentRegistry.cs`, replace the four transition methods and add a private helper:

```csharp
public Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default)
    => TransitionAsync(id, AgentStatus.Healthy, ct);

public Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default)
    => TransitionAsync(id, AgentStatus.Degraded, ct);

public Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default)
    => TransitionAsync(id, AgentStatus.Offline, ct);

public Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default)
    => TransitionAsync(id, AgentStatus.Draining, ct);

private async Task<Result> TransitionAsync(AgentId id, AgentStatus target, CancellationToken ct)
{
    var maybe = await _store.GetAsync(id, ct);
    if (maybe.HasNoValue)
        return Result.Failure(RegistryErrors.AgentNotFound);

    if (!IsLegalTransition(maybe.Value.Status, target))
        return Result.Failure(RegistryErrors.InvalidStatusTransition);

    return await _store.UpdateStatusAsync(id, target, ct);
}

private static bool IsLegalTransition(AgentStatus from, AgentStatus to) => (from, to) switch
{
    (AgentStatus.Starting, AgentStatus.Healthy)  => true,
    (AgentStatus.Starting, AgentStatus.Offline)  => true,

    (AgentStatus.Healthy,  AgentStatus.Degraded) => true,
    (AgentStatus.Healthy,  AgentStatus.Offline)  => true,
    (AgentStatus.Healthy,  AgentStatus.Draining) => true,

    (AgentStatus.Degraded, AgentStatus.Healthy)  => true,
    (AgentStatus.Degraded, AgentStatus.Offline)  => true,
    (AgentStatus.Degraded, AgentStatus.Draining) => true,

    (AgentStatus.Draining, AgentStatus.Offline)  => true,

    _ => false
};
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryTransitionTests`
Expected: 21 passed (9 legal + 11 illegal + 1 not-found).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Runtime/Registry/AgentRegistry.cs tests/Sigil.Runtime.Tests/Registry/AgentRegistryTransitionTests.cs
git commit -m "feat(runtime): AgentRegistry status transitions with matrix enforcement"
```

---

## Task 13 — Implement `SelectByWeightAsync` (TDD)

**Files**
- Create: `tests/Sigil.Runtime.Tests/Registry/AgentRegistryWeightedSelectionTests.cs`
- Modify: `src/Sigil.Runtime/Registry/AgentRegistry.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryWeightedSelectionTests
{
    private static AgentRegistry Make(FakeAgentRegistrationStore store, IRandomProvider random)
        => new(store, random);

    [Fact]
    public async Task Empty_skill_name_returns_failure_via_Maybe_None_and_throws_no()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = Make(store, new StubRandomProvider(seed: 1));

        // SkillNameRequired surfaces as ArgumentException for invalid input.
        await Should.ThrowAsync<ArgumentException>(
            () => registry.SelectByWeightAsync("  "));
    }

    [Fact]
    public async Task No_agents_for_skill_returns_None()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task All_candidates_unhealthy_returns_None()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("a", status: AgentStatus.Degraded));
        await store.RegisterAsync(TestAgents.Make("b", status: AgentStatus.Offline));
        await store.RegisterAsync(TestAgents.Make("c", status: AgentStatus.Draining));
        await store.RegisterAsync(TestAgents.Make("d", status: AgentStatus.Starting));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Zero_weight_candidates_are_excluded()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("zero", status: AgentStatus.Healthy, routingWeight: 0));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Single_healthy_candidate_is_always_selected()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("solo", status: AgentStatus.Healthy, routingWeight: 5));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeTrue();
        pick.Value.AgentId.Value.ShouldBe("solo");
    }

    [Fact]
    public async Task Weighted_distribution_matches_weights_within_tolerance()
    {
        var store = new FakeAgentRegistrationStore();
        // Deterministic order by AgentId: "canary" < "stable"
        await store.RegisterAsync(TestAgents.Make("canary", status: AgentStatus.Healthy, routingWeight: 10));
        await store.RegisterAsync(TestAgents.Make("stable", status: AgentStatus.Healthy, routingWeight: 90));

        var registry = Make(store, new StubRandomProvider(seed: 42));

        const int draws = 10_000;
        var counts = new Dictionary<string, int> { ["canary"] = 0, ["stable"] = 0 };
        for (var i = 0; i < draws; i++)
        {
            var pick = await registry.SelectByWeightAsync("echo");
            pick.HasValue.ShouldBeTrue();
            counts[pick.Value.AgentId.Value]++;
        }

        var canaryRatio = counts["canary"] / (double)draws;
        canaryRatio.ShouldBeInRange(0.08, 0.12); // 10% ± 2 pp
    }

    [Fact]
    public async Task Deterministic_pick_uses_running_total_against_roll()
    {
        // Order: "a" (w=10), "b" (w=20), "c" (w=70). Total = 100.
        // Queued rolls: 5 (< 10 → a), 25 (10 ≤ 25 < 30 → b), 95 (30 ≤ 95 < 100 → c)
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("a", status: AgentStatus.Healthy, routingWeight: 10));
        await store.RegisterAsync(TestAgents.Make("b", status: AgentStatus.Healthy, routingWeight: 20));
        await store.RegisterAsync(TestAgents.Make("c", status: AgentStatus.Healthy, routingWeight: 70));
        var registry = Make(store, new StubRandomProvider(new[] { 5, 25, 95 }));

        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("a");
        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("b");
        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("c");
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryWeightedSelectionTests`
Expected: `NotImplementedException` for all.

- [ ] **Step 3: Implement `SelectByWeightAsync`**

Replace `SelectByWeightAsync` in `src/Sigil.Runtime/Registry/AgentRegistry.cs`:

```csharp
public async Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(skillName))
        throw new ArgumentException(RegistryErrors.SkillNameRequired, nameof(skillName));

    var candidates = (await _store.FindBySkillAsync(skillName, ct))
        .Where(a => a.Status == AgentStatus.Healthy && a.RoutingWeight > 0)
        .OrderBy(a => a.AgentId.Value, StringComparer.Ordinal)
        .ToList();

    if (candidates.Count == 0)
        return Maybe<AgentRegistration>.None;

    var total = candidates.Sum(c => c.RoutingWeight);
    var roll = _random.Next(total);
    var running = 0;
    foreach (var c in candidates)
    {
        running += c.RoutingWeight;
        if (roll < running)
            return Maybe.From(c);
    }

    // Unreachable when total > 0 and roll < total, but the compiler doesn't know.
    return Maybe.From(candidates[^1]);
}
```

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test tests/Sigil.Runtime.Tests/Sigil.Runtime.Tests.csproj --filter FullyQualifiedName~AgentRegistryWeightedSelectionTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Runtime/Registry/AgentRegistry.cs tests/Sigil.Runtime.Tests/Registry/AgentRegistryWeightedSelectionTests.cs
git commit -m "feat(runtime): AgentRegistry weighted selection over healthy candidates"
```

---

## Task 14 — Add `AddSigilRuntime` DI extension

**Files**
- Create: `src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the file**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;

namespace Sigil.Runtime.DependencyInjection;

public static class SigilRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers runtime services: <see cref="IAgentRegistry"/> and its dependencies.
    /// Requires <c>IAgentRegistrationStore</c> to be registered separately by the chosen storage provider.
    /// </summary>
    public static IServiceCollection AddSigilRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IRandomProvider, SystemRandomProvider>();
        services.AddScoped<IAgentRegistry, AgentRegistry>();
        return services;
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Sigil.Runtime/Sigil.Runtime.csproj`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs
git commit -m "feat(runtime): add AddSigilRuntime DI extension"
```

---

## Task 15 — Full solution build + test verification

- [ ] **Step 1: Clean build**

Run: `dotnet build sigil.sln`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 2: Full test suite**

Run: `dotnet test sigil.sln`
Expected: All tests pass. New test counts:
- `Sigil.Core.Tests` includes 1 new `RegistryErrorsTests`.
- `Sigil.Runtime.Tests` reports ~38 tests across registration (4) + heartbeat (6) + transitions (20) + weighted selection (7) + 1 not-found.

- [ ] **Step 3: No commit** — verification only.

---

## Task 16 — Update Roadmap status

**Files**
- Modify: `Roadmap.md`

- [ ] **Step 1: Inspect roadmap row**

Run: `grep -n "#7" Roadmap.md`
Expected: a row like `| ⬜ | [#7](...) | Secure Agent Registry with weighted routing | #2, #4 |`.

- [ ] **Step 2: Flip status to 🔄 (in flight)**

Use the Edit tool to change the leading `⬜` on the issue #7 row to `🔄` (the PR will mark it `✅` on merge — same convention used by prior issues).

- [ ] **Step 3: Commit**

```bash
git add Roadmap.md
git commit -m "chore(docs): mark Roadmap #7 as in flight"
```

---

## Task 17 — Push branch and open PR

- [ ] **Step 1: Push branch**

Run: `git push -u origin feat/agent-registry`

- [ ] **Step 2: Open PR**

Run:

```bash
gh pr create --title "feat(runtime): Agent Registry with weighted routing (closes #7)" --body "$(cat <<'EOF'
## Summary
- Adds `IAgentRegistry` (Sigil.Core) and `AgentRegistry` (Sigil.Runtime) over `IAgentRegistrationStore`.
- Enforces `AgentStatus` transition matrix; `HeartbeatAsync` promotes Starting/Degraded → Healthy and rejects Offline.
- Implements weighted random selection over Healthy candidates with `RoutingWeight > 0`, deterministic ordering by `AgentId`.
- Introduces `IRandomProvider` / `SystemRandomProvider` so distribution tests are deterministic.
- New test project `tests/Sigil.Runtime.Tests` with an in-memory store fake and full transition coverage.

## Out of scope (deferred)
- `DeregisterAsync`, stale-row cleanup, heartbeat-sweep timing → #11
- Registration/heartbeat HTTP endpoints → #13

## Test plan
- [x] `dotnet build sigil.sln` clean (warnings-as-errors)
- [x] `dotnet test sigil.sln` green
- [x] Status transition matrix exhaustively pinned (legal + illegal cases)
- [x] Weighted distribution within ±2 pp of weights over 10k draws (seeded)

Spec: `docs/superpowers/specs/2026-05-13-issue-07-agent-registry-design.md`
Plan: `docs/superpowers/plans/2026-05-13-issue-07-agent-registry.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR URL printed.

- [ ] **Step 3: Report PR URL to the user.**

---

## Self-review notes

- Spec §2 scope coverage: ✅ all in-scope files appear as tasks; out-of-scope items called out in PR body.
- Spec §3 design decisions: ✅ Q1–Q10 each reflected in tasks 4–13.
- Spec §4 interface signature: ✅ matches Task 4 verbatim.
- Spec §5 transition matrix: ✅ tasks 11 (heartbeat) and 12 (explicit transitions) together cover every cell from §5.
- Spec §6 algorithm: ✅ Task 13 implements ordering + running-total walk with deterministic provider tests.
- Spec §7 test inventory: ✅ each listed test is present (registration ×4, heartbeat ×6, transitions ×20, selection ×7, errors-constants ×1).
- Spec §8 DI: ✅ Task 14.
- Spec §9 verification: ✅ Task 15.
- Type consistency: `IAgentRegistry` methods used in tests match the interface (`MarkHealthyAsync` / `MarkDegradedAsync` / `MarkOfflineAsync` / `BeginDrainingAsync` / `HeartbeatAsync` / `SelectByWeightAsync`). `IRandomProvider.Next(int)` used consistently. `RegistryErrors.*` constants spelled identically across producer and tests.
- No placeholders, no "similar to Task N", every code step shows the full code to write.
