# Issue #2 — Core Storage Contracts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the storage contracts, immutable audit contract, identity types, and the protocol records the storage surface depends on, in `Sigil.Core`. No concrete providers, no behaviour — pure contract layer.

**Architecture:** `Sigil.Core` takes one non-BCL dep (`CSharpFunctionalExtensions`) so `Result` and `Maybe<T>` are the native vocabulary. Strongly-typed identifiers (`JobId`, `AgentId`, `StepId`, `ETag`) replace raw strings. Snapshots are immutable (`IReadOnlyDictionary`); deltas stay mutable for builder ergonomics. Every async method on the contract layer takes `CancellationToken`. The matching blueprint code snippets are updated in the same PR.

**Tech Stack:** .NET 9, C# 13, `CSharpFunctionalExtensions` (Result / Maybe), xUnit, FluentAssertions, `System.Text.Json`.

**Branch:** `feat/core-storage-contracts` (already checked out)

**Design spec:** `docs/superpowers/specs/2026-04-19-issue-2-core-storage-contracts-design.md`

**Blueprint:** `.bob/docs/sigil-architecture-blueprint.md` §3, §4.6

---

## File Structure

### Created

```
src/Sigil.Core/
├── Identity/
│   ├── AgentId.cs
│   ├── JobId.cs
│   ├── StepId.cs
│   └── ETag.cs
├── Protocol/
│   ├── ContextSnapshot.cs
│   ├── ContextDelta.cs
│   ├── UsageMetrics.cs
│   └── AgentLogEntry.cs
├── Registry/
│   ├── AgentRegistration.cs
│   ├── Capability.cs
│   ├── SecurityProfile.cs
│   ├── AgentStatus.cs
│   └── AgentMetadata.cs
├── Jobs/
│   └── Job.cs                    # minimal — just what store interfaces need
├── Checkpoints/
│   └── Checkpoint.cs             # minimal — just what store interfaces need
├── Audit/
│   └── AuditEntry.cs
└── Storage/
    ├── ISigilStore.cs
    ├── IAgentRegistrationStore.cs
    ├── IJobStore.cs
    ├── IContextStore.cs
    ├── ICheckpointStore.cs
    └── IAuditStore.cs

tests/Sigil.Core.Tests/
├── Sigil.Core.Tests.csproj
├── Identity/
│   └── IdentityTypesTests.cs
├── Protocol/
│   ├── ContextSnapshotTests.cs
│   ├── ContextDeltaTests.cs
│   ├── UsageMetricsTests.cs
│   └── AgentLogEntryTests.cs
├── Audit/
│   └── AuditEntryTests.cs
└── Protocol/
    └── JsonRoundTripTests.cs
```

### Modified

- `src/Sigil.Core/Sigil.Core.csproj` — add `CSharpFunctionalExtensions` package reference.
- `sigil.sln` — add `Sigil.Core.Tests` project.
- `.bob/docs/sigil-architecture-blueprint.md` — update code blocks in §3 (`IContextStore`, `IAgentRegistrationStore`) and §4.6 (`ISigilStore`, `IAuditStore`, `AuditEntry`) to match landed code.

### File responsibilities — one thing per file

- **Identity types** (4 files) — each defines one strongly-typed identifier as a `readonly record struct`.
- **Protocol records** (4 files) — wire-level shapes used by storage and (later) by agent protocol endpoints.
- **Registry records** (5 files) — `AgentRegistration` and its supporting types. Pulled from blueprint §3, updated to use `AgentId` / `JobId` where applicable.
- **Jobs, Checkpoints** — minimal placeholder records so store interfaces compile. Full shape lands with the respective features.
- **Audit record** — one file, one record.
- **Storage interfaces** — one interface per file.
- **Tests** — grouped by subject area.

---

## Task 1: Project setup — `CSharpFunctionalExtensions` dep + test project

**Files:**
- Modify: `src/Sigil.Core/Sigil.Core.csproj`
- Create: `tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj`
- Modify: `sigil.sln`

- [ ] **Step 1: Add `CSharpFunctionalExtensions` package to `Sigil.Core`**

Run from repo root:

```bash
dotnet add src/Sigil.Core/Sigil.Core.csproj package CSharpFunctionalExtensions
```

Expected: package added, `Sigil.Core.csproj` updated with `<PackageReference Include="CSharpFunctionalExtensions" Version="..." />`.

- [ ] **Step 2: Verify build**

```bash
dotnet build sigil.sln
```

Expected: build succeeds, 0 warnings, 0 errors.

- [ ] **Step 3: Create test project directory and csproj**

```bash
mkdir -p tests/Sigil.Core.Tests
```

Create `tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Sigil.Core\Sigil.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add test project to the solution**

```bash
dotnet sln sigil.sln add tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj
```

Expected: project added to `sigil.sln`.

- [ ] **Step 5: Verify the solution builds with the test project**

```bash
dotnet build sigil.sln
```

Expected: build succeeds, 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Core/Sigil.Core.csproj tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj sigil.sln
git commit -m "chore(core): add CSharpFunctionalExtensions dep and test project scaffold"
```

---

## Task 2: Identity types — `AgentId`, `JobId`, `StepId`, `ETag`

These four types are structurally identical. Test them together.

**Files:**
- Create: `src/Sigil.Core/Identity/AgentId.cs`
- Create: `src/Sigil.Core/Identity/JobId.cs`
- Create: `src/Sigil.Core/Identity/StepId.cs`
- Create: `src/Sigil.Core/Identity/ETag.cs`
- Create: `tests/Sigil.Core.Tests/Identity/IdentityTypesTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Sigil.Core.Tests/Identity/IdentityTypesTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Identity;
using Xunit;

namespace Sigil.Core.Tests.Identity;

public class IdentityTypesTests
{
    [Fact]
    public void AgentId_WithSameValue_AreEqual()
    {
        var a = new AgentId("agent-1");
        var b = new AgentId("agent-1");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AgentId_WithDifferentValue_AreNotEqual()
    {
        var a = new AgentId("agent-1");
        var b = new AgentId("agent-2");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void AgentId_ToString_ReturnsRawValue()
    {
        var a = new AgentId("agent-xyz");

        a.ToString().Should().Be("agent-xyz");
    }

    [Fact]
    public void JobId_ToString_ReturnsRawValue()
    {
        new JobId("job-1").ToString().Should().Be("job-1");
    }

    [Fact]
    public void StepId_ToString_ReturnsRawValue()
    {
        new StepId("step-1").ToString().Should().Be("step-1");
    }

    [Fact]
    public void ETag_ToString_ReturnsRawValue()
    {
        new ETag("abc123").ToString().Should().Be("abc123");
    }

    [Fact]
    public void DistinctIdTypes_AreNotImplicitlyConvertible()
    {
        // Compile-time check: this test exists to document intent.
        // If AgentId and JobId ever become implicitly convertible,
        // the assignment below will compile — which is the failure mode.
        var agentId = new AgentId("x");
        var jobId = new JobId("x");

        // Values may coincide, but types must not be interchangeable.
        agentId.Value.Should().Be(jobId.Value);
        agentId.GetType().Should().NotBe(jobId.GetType());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj
```

Expected: compile errors — `AgentId`, `JobId`, `StepId`, `ETag` not defined.

- [ ] **Step 3: Implement the four identity types**

Create `src/Sigil.Core/Identity/AgentId.cs`:

```csharp
namespace Sigil.Core.Identity;

public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
```

Create `src/Sigil.Core/Identity/JobId.cs`:

```csharp
namespace Sigil.Core.Identity;

public readonly record struct JobId(string Value)
{
    public override string ToString() => Value;
}
```

Create `src/Sigil.Core/Identity/StepId.cs`:

```csharp
namespace Sigil.Core.Identity;

public readonly record struct StepId(string Value)
{
    public override string ToString() => Value;
}
```

Create `src/Sigil.Core/Identity/ETag.cs`:

```csharp
namespace Sigil.Core.Identity;

public readonly record struct ETag(string Value)
{
    public override string ToString() => Value;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Identity/ tests/Sigil.Core.Tests/Identity/
git commit -m "feat(core): add strongly-typed identifiers (AgentId, JobId, StepId, ETag)"
```

---

## Task 3: `ContextDelta` record

**Files:**
- Create: `src/Sigil.Core/Protocol/ContextDelta.cs`
- Create: `tests/Sigil.Core.Tests/Protocol/ContextDeltaTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Protocol/ContextDeltaTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ContextDeltaTests
{
    [Fact]
    public void Default_HasEmptyUpdatesAndRemovals()
    {
        var delta = new ContextDelta();

        delta.Updates.Should().BeEmpty();
        delta.Removals.Should().BeEmpty();
    }

    [Fact]
    public void CanPopulateUpdatesAfterConstruction()
    {
        var delta = new ContextDelta();
        delta.Updates["key"] = "value";

        delta.Updates.Should().ContainKey("key");
    }

    [Fact]
    public void DeltasWithSameContent_AreEqualByRecordSemantics()
    {
        var a = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r"]
        };
        var b = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r"]
        };

        // Note: records compare by reference for reference-type properties by default.
        // This test asserts that two distinct deltas with the same *dictionary reference*
        // are equal — the Updates dictionary is the same instance here.
        var shared = new Dictionary<string, object> { ["k"] = "v" };
        var sharedR = new[] { "r" };
        var c = new ContextDelta { Updates = shared, Removals = sharedR };
        var d = new ContextDelta { Updates = shared, Removals = sharedR };

        c.Should().Be(d);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~ContextDeltaTests"
```

Expected: compile errors — `ContextDelta` not defined.

- [ ] **Step 3: Implement the record**

Create `src/Sigil.Core/Protocol/ContextDelta.cs`:

```csharp
namespace Sigil.Core.Protocol;

public sealed record ContextDelta
{
    public Dictionary<string, object> Updates { get; init; } = new();
    public string[] Removals { get; init; } = [];
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~ContextDeltaTests"
```

Expected: all 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Protocol/ContextDelta.cs tests/Sigil.Core.Tests/Protocol/ContextDeltaTests.cs
git commit -m "feat(core): add ContextDelta record"
```

---

## Task 4: `ContextSnapshot` record with `Maybe<T>`-returning `Get<T>`

**Files:**
- Create: `src/Sigil.Core/Protocol/ContextSnapshot.cs`
- Create: `tests/Sigil.Core.Tests/Protocol/ContextSnapshotTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Protocol/ContextSnapshotTests.cs`:

```csharp
using CSharpFunctionalExtensions;
using FluentAssertions;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ContextSnapshotTests
{
    [Fact]
    public void Default_StateIsEmpty()
    {
        var snap = new ContextSnapshot();

        snap.State.Should().BeEmpty();
    }

    [Fact]
    public void JobId_RoundTrips()
    {
        var snap = new ContextSnapshot { JobId = new JobId("job-1") };

        snap.JobId.Should().Be(new JobId("job-1"));
    }

    [Fact]
    public void Get_WhenKeyPresentAndTypeMatches_ReturnsMaybeFromValue()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = 42 }
        };

        var result = snap.Get<int>("count");

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Get_WhenKeyAbsent_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot();

        var result = snap.Get<int>("missing");

        result.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Get_WhenKeyPresentButWrongType_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = "not-an-int" }
        };

        var result = snap.Get<int>("count");

        result.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void State_IsReadOnlyDictionary()
    {
        var snap = new ContextSnapshot();

        // Compile-time guarantee: the type of State is IReadOnlyDictionary<string, object>.
        // Regression check — if someone widens it to Dictionary<string, object>, this fails.
        typeof(ContextSnapshot).GetProperty("State")!.PropertyType
            .Should().Be(typeof(IReadOnlyDictionary<string, object>));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~ContextSnapshotTests"
```

Expected: compile errors — `ContextSnapshot` not defined.

- [ ] **Step 3: Implement the record**

Create `src/Sigil.Core/Protocol/ContextSnapshot.cs`:

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record ContextSnapshot
{
    public JobId JobId { get; init; }

    public IReadOnlyDictionary<string, object> State { get; init; }
        = new Dictionary<string, object>();

    public Maybe<T> Get<T>(string key) =>
        State.TryGetValue(key, out var val) && val is T typed
            ? Maybe.From(typed)
            : Maybe<T>.None;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~ContextSnapshotTests"
```

Expected: all 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Protocol/ContextSnapshot.cs tests/Sigil.Core.Tests/Protocol/ContextSnapshotTests.cs
git commit -m "feat(core): add ContextSnapshot with Maybe<T>-returning Get helper"
```

---

## Task 5: `UsageMetrics` record

**Files:**
- Create: `src/Sigil.Core/Protocol/UsageMetrics.cs`
- Create: `tests/Sigil.Core.Tests/Protocol/UsageMetricsTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Protocol/UsageMetricsTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class UsageMetricsTests
{
    [Fact]
    public void Default_HasZeroTokensAndEmptyCustom()
    {
        var m = new UsageMetrics();

        m.PromptTokens.Should().Be(0);
        m.CompletionTokens.Should().Be(0);
        m.Duration.Should().Be(TimeSpan.Zero);
        m.Custom.Should().BeEmpty();
    }

    [Fact]
    public void InitProperties_Roundtrip()
    {
        var m = new UsageMetrics
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            Duration = TimeSpan.FromSeconds(1.5),
            Custom = new Dictionary<string, object> { ["model"] = "claude-sonnet-4-6" }
        };

        m.PromptTokens.Should().Be(10);
        m.CompletionTokens.Should().Be(20);
        m.Duration.Should().Be(TimeSpan.FromSeconds(1.5));
        m.Custom.Should().ContainKey("model");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~UsageMetricsTests"
```

Expected: compile errors — `UsageMetrics` not defined.

- [ ] **Step 3: Implement the record**

Create `src/Sigil.Core/Protocol/UsageMetrics.cs`:

```csharp
namespace Sigil.Core.Protocol;

public sealed record UsageMetrics
{
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyDictionary<string, object> Custom { get; init; }
        = new Dictionary<string, object>();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~UsageMetricsTests"
```

Expected: both tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Protocol/UsageMetrics.cs tests/Sigil.Core.Tests/Protocol/UsageMetricsTests.cs
git commit -m "feat(core): add UsageMetrics record"
```

---

## Task 6: `AgentLogEntry` record

**Files:**
- Create: `src/Sigil.Core/Protocol/AgentLogEntry.cs`
- Create: `tests/Sigil.Core.Tests/Protocol/AgentLogEntryTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Protocol/AgentLogEntryTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class AgentLogEntryTests
{
    [Fact]
    public void Default_HasInfoLevelAndEmptyMessageAndUtcTimestamp()
    {
        var before = DateTime.UtcNow;
        var entry = new AgentLogEntry();
        var after = DateTime.UtcNow;

        entry.Level.Should().Be("Info");
        entry.Message.Should().Be("");
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entry.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void InitProperties_Roundtrip()
    {
        var ts = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);
        var entry = new AgentLogEntry
        {
            Timestamp = ts,
            AgentId = new AgentId("agent-1"),
            Level = "Warn",
            Message = "hello"
        };

        entry.Timestamp.Should().Be(ts);
        entry.AgentId.Should().Be(new AgentId("agent-1"));
        entry.Level.Should().Be("Warn");
        entry.Message.Should().Be("hello");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AgentLogEntryTests"
```

Expected: compile errors — `AgentLogEntry` not defined.

- [ ] **Step 3: Implement the record**

Create `src/Sigil.Core/Protocol/AgentLogEntry.cs`:

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record AgentLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AgentId AgentId { get; init; }
    public string Level { get; init; } = "Info";
    public string Message { get; init; } = "";
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AgentLogEntryTests"
```

Expected: both tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Protocol/AgentLogEntry.cs tests/Sigil.Core.Tests/Protocol/AgentLogEntryTests.cs
git commit -m "feat(core): add AgentLogEntry record"
```

---

## Task 7: `AuditEntry` record

**Files:**
- Create: `src/Sigil.Core/Audit/AuditEntry.cs`
- Create: `tests/Sigil.Core.Tests/Audit/AuditEntryTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Audit/AuditEntryTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Audit;

public class AuditEntryTests
{
    [Fact]
    public void Default_GeneratesAuditIdAndUtcTimestamp()
    {
        var before = DateTime.UtcNow;
        var entry = new AuditEntry();
        var after = DateTime.UtcNow;

        entry.AuditId.Should().NotBeNullOrWhiteSpace();
        entry.AuditId.Length.Should().Be(32); // Guid "N" format
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entry.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void TwoDefaultEntries_HaveDifferentAuditIds()
    {
        var a = new AuditEntry();
        var b = new AuditEntry();

        a.AuditId.Should().NotBe(b.AuditId);
    }

    [Fact]
    public void TwoEntriesWithIdenticalFields_AreEqual()
    {
        var delta = new ContextDelta();
        var metrics = new UsageMetrics();

        var a = new AuditEntry
        {
            AuditId = "fixed-id",
            JobId = new JobId("j"),
            AgentId = new AgentId("a"),
            StepId = new StepId("s"),
            Delta = delta,
            Metrics = metrics,
            Timestamp = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var b = a with { };

        a.Should().Be(b);
    }

    [Fact]
    public void TwoEntriesDifferingInOneField_AreNotEqual()
    {
        var a = new AuditEntry { AuditId = "x", JobId = new JobId("j1") };
        var b = a with { JobId = new JobId("j2") };

        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AuditEntryTests"
```

Expected: compile errors — `AuditEntry` not defined.

- [ ] **Step 3: Implement the record**

Create `src/Sigil.Core/Audit/AuditEntry.cs`:

```csharp
using Sigil.Core.Identity;
using Sigil.Core.Protocol;

namespace Sigil.Core.Audit;

public sealed record AuditEntry
{
    public string AuditId { get; init; } = Guid.NewGuid().ToString("N");
    public JobId JobId { get; init; }
    public AgentId AgentId { get; init; }
    public StepId StepId { get; init; }
    public ContextDelta Delta { get; init; } = new();
    public UsageMetrics Metrics { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AuditEntryTests"
```

Expected: all 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Audit/ tests/Sigil.Core.Tests/Audit/
git commit -m "feat(core): add immutable AuditEntry record"
```

---

## Task 8: Registry records — `AgentRegistration` + supporting types

Pulled verbatim from blueprint §3, with `AgentId` used instead of `string` for the `AgentId` field. No behaviour, no tests beyond compile-success (records are exercised through store interface tests later).

**Files:**
- Create: `src/Sigil.Core/Registry/AgentStatus.cs`
- Create: `src/Sigil.Core/Registry/Capability.cs`
- Create: `src/Sigil.Core/Registry/SecurityProfile.cs`
- Create: `src/Sigil.Core/Registry/AgentMetadata.cs`
- Create: `src/Sigil.Core/Registry/AgentRegistration.cs`

- [ ] **Step 1: Create `AgentStatus.cs`**

```csharp
namespace Sigil.Core.Registry;

public enum AgentStatus
{
    Starting,
    Healthy,
    Degraded,
    Offline,
    Draining
}
```

- [ ] **Step 2: Create `Capability.cs`**

```csharp
namespace Sigil.Core.Registry;

public sealed record Capability
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string[] RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
}
```

- [ ] **Step 3: Create `SecurityProfile.cs`**

```csharp
namespace Sigil.Core.Registry;

public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public string[] AllowedTools { get; init; } = [];
}
```

- [ ] **Step 4: Create `AgentMetadata.cs`**

```csharp
namespace Sigil.Core.Registry;

public sealed record AgentMetadata
{
    public int? MaxTokenBudget { get; init; }
    public string? Model { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
```

- [ ] **Step 5: Create `AgentRegistration.cs`**

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

public sealed record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public string Name { get; init; } = default!;
    public string Domain { get; init; } = default!;
    public IReadOnlyList<Capability> Capabilities { get; init; } = [];
    public string SemanticVersion { get; init; } = "1.0.0";
    public string EndpointUrl { get; init; } = default!;
    public int RoutingWeight { get; init; } = 100;
    public AgentStatus Status { get; init; } = AgentStatus.Starting;
    public SecurityProfile Security { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
    public AgentMetadata Metadata { get; init; } = new();
}
```

- [ ] **Step 6: Verify build**

```bash
dotnet build sigil.sln
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Sigil.Core/Registry/
git commit -m "feat(core): add AgentRegistration and supporting registry records"
```

---

## Task 9: Minimal `Job` and `Checkpoint` records

These are placeholders so `IJobStore` and `ICheckpointStore` compile with real types. Full shapes belong to later issues — hence the deliberate minimalism.

**Files:**
- Create: `src/Sigil.Core/Jobs/Job.cs`
- Create: `src/Sigil.Core/Jobs/JobStatus.cs`
- Create: `src/Sigil.Core/Checkpoints/Checkpoint.cs`
- Create: `src/Sigil.Core/Checkpoints/CheckpointStatus.cs`

- [ ] **Step 1: Create `JobStatus.cs`**

```csharp
namespace Sigil.Core.Jobs;

public enum JobStatus
{
    Pending,
    Running,
    AwaitingCheckpoint,
    Completed,
    Failed,
    Cancelled
}
```

- [ ] **Step 2: Create `Job.cs`**

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Jobs;

public sealed record Job
{
    public JobId JobId { get; init; }
    public JobStatus Status { get; init; } = JobStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Create `CheckpointStatus.cs`**

```csharp
namespace Sigil.Core.Checkpoints;

public enum CheckpointStatus
{
    Pending,
    Approved,
    Rejected
}
```

- [ ] **Step 4: Create `Checkpoint.cs`**

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Checkpoints;

public sealed record Checkpoint
{
    public string CheckpointId { get; init; } = Guid.NewGuid().ToString("N");
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public CheckpointStatus Status { get; init; } = CheckpointStatus.Pending;
    public string? ResolvedBy { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; init; }
}
```

- [ ] **Step 5: Verify build**

```bash
dotnet build sigil.sln
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Core/Jobs/ src/Sigil.Core/Checkpoints/
git commit -m "feat(core): add minimal Job and Checkpoint records for store interface shape"
```

---

## Task 10: Storage interfaces

All six interfaces land together. They have no behaviour to unit-test; the compile-time check is sufficient here. Storage providers (#5, #6) will exercise them end-to-end.

**Files:**
- Create: `src/Sigil.Core/Storage/IContextStore.cs`
- Create: `src/Sigil.Core/Storage/IAuditStore.cs`
- Create: `src/Sigil.Core/Storage/IAgentRegistrationStore.cs`
- Create: `src/Sigil.Core/Storage/IJobStore.cs`
- Create: `src/Sigil.Core/Storage/ICheckpointStore.cs`
- Create: `src/Sigil.Core/Storage/ISigilStore.cs`

- [ ] **Step 1: Create `IContextStore.cs`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;

namespace Sigil.Core.Storage;

public interface IContextStore
{
    Task<Result<(ContextSnapshot Snapshot, ETag ETag)>> GetSnapshotAsync(
        JobId jobId,
        CancellationToken ct = default);

    Task<Result> CommitDeltaAsync(
        JobId jobId,
        ContextDelta delta,
        ETag expectedETag,
        CancellationToken ct = default);

    Task AppendLogAsync(
        JobId jobId,
        AgentLogEntry entry,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentLogEntry>> GetLogAsync(
        JobId jobId,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `IAuditStore.cs`**

```csharp
using Sigil.Core.Audit;
using Sigil.Core.Identity;

namespace Sigil.Core.Storage;

public interface IAuditStore
{
    Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
        JobId jobId, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(
        AgentId agentId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create `IAgentRegistrationStore.cs`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Core.Storage;

public interface IAgentRegistrationStore
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(
        string capabilityName, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(
        string domain, CancellationToken ct = default);

    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);

    Task<Result> UpdateStatusAsync(
        AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create `IJobStore.cs`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;

namespace Sigil.Core.Storage;

public interface IJobStore
{
    Task<Result> CreateAsync(Job job, CancellationToken ct = default);

    Task<Maybe<Job>> GetAsync(JobId jobId, CancellationToken ct = default);

    Task<Result> UpdateStatusAsync(
        JobId jobId, JobStatus status, CancellationToken ct = default);
}
```

- [ ] **Step 5: Create `ICheckpointStore.cs`**

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Checkpoints;
using Sigil.Core.Identity;

namespace Sigil.Core.Storage;

public interface ICheckpointStore
{
    Task<Result> CreateAsync(Checkpoint checkpoint, CancellationToken ct = default);

    Task<Maybe<Checkpoint>> GetAsync(string checkpointId, CancellationToken ct = default);

    Task<IReadOnlyList<Checkpoint>> GetPendingForJobAsync(
        JobId jobId, CancellationToken ct = default);

    Task<Result> ResolveAsync(
        string checkpointId,
        CheckpointStatus status,
        string resolvedBy,
        CancellationToken ct = default);
}
```

- [ ] **Step 6: Create `ISigilStore.cs`**

```csharp
namespace Sigil.Core.Storage;

public interface ISigilStore
{
    IAgentRegistrationStore Agents { get; }
    IJobStore Jobs { get; }
    IContextStore Contexts { get; }
    ICheckpointStore Checkpoints { get; }
    IAuditStore Audit { get; }
}
```

- [ ] **Step 7: Verify build**

```bash
dotnet build sigil.sln
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/Sigil.Core/Storage/
git commit -m "feat(core): add ISigilStore aggregate and five sub-store interfaces"
```

---

## Task 11: JSON round-trip tests

Tests that `System.Text.Json` can serialise and deserialise the public contract records. Downstream storage providers rely on this stability.

**Files:**
- Create: `tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`

- [ ] **Step 1: Write failing test**

Create `tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class JsonRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        IncludeFields = false
    };

    [Fact]
    public void AgentId_RoundTrips()
    {
        var original = new AgentId("agent-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentId>(json, Options);

        back.Should().Be(original);
    }

    [Fact]
    public void ContextDelta_RoundTrips()
    {
        var original = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r1", "r2"]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ContextDelta>(json, Options)!;

        back.Updates.Should().ContainKey("k");
        back.Removals.Should().Equal("r1", "r2");
    }

    [Fact]
    public void UsageMetrics_RoundTrips()
    {
        var original = new UsageMetrics
        {
            PromptTokens = 100,
            CompletionTokens = 200,
            Duration = TimeSpan.FromSeconds(2.5)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<UsageMetrics>(json, Options)!;

        back.PromptTokens.Should().Be(100);
        back.CompletionTokens.Should().Be(200);
        back.Duration.Should().Be(TimeSpan.FromSeconds(2.5));
    }

    [Fact]
    public void AgentLogEntry_RoundTrips()
    {
        var original = new AgentLogEntry
        {
            Timestamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            AgentId = new AgentId("agent-1"),
            Level = "Info",
            Message = "hello"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentLogEntry>(json, Options)!;

        back.Timestamp.Should().Be(original.Timestamp);
        back.AgentId.Should().Be(original.AgentId);
        back.Level.Should().Be("Info");
        back.Message.Should().Be("hello");
    }

    [Fact]
    public void AuditEntry_RoundTrips()
    {
        var original = new AuditEntry
        {
            AuditId = "fixed-audit-id",
            JobId = new JobId("j-1"),
            AgentId = new AgentId("a-1"),
            StepId = new StepId("s-1"),
            Delta = new ContextDelta { Removals = ["k"] },
            Metrics = new UsageMetrics { PromptTokens = 5 },
            Timestamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AuditEntry>(json, Options)!;

        back.AuditId.Should().Be("fixed-audit-id");
        back.JobId.Should().Be(original.JobId);
        back.AgentId.Should().Be(original.AgentId);
        back.StepId.Should().Be(original.StepId);
        back.Delta.Removals.Should().Equal("k");
        back.Metrics.PromptTokens.Should().Be(5);
        back.Timestamp.Should().Be(original.Timestamp);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~JsonRoundTripTests"
```

Expected: all 5 tests pass. If `AgentId` fails to round-trip (because record structs with a single `Value` property serialise as objects), that's informative — move to Step 3.

- [ ] **Step 3: If `AgentId_RoundTrips` fails, add a `JsonConverter`**

If the round-trip test for `AgentId` fails (typical default behaviour serialises it as `{"Value":"agent-1"}` but may fail to deserialise it back into a record struct constructor depending on runtime), add a simple converter.

Create `src/Sigil.Core/Identity/AgentIdJsonConverter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class AgentIdJsonConverter : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
```

Decorate the struct:

```csharp
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

[JsonConverter(typeof(AgentIdJsonConverter))]
public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
```

Repeat the same pattern for `JobId`, `StepId`, `ETag` (create `JobIdJsonConverter`, `StepIdJsonConverter`, `ETagJsonConverter`, decorate their structs). The converter is a 6-line class per type.

Re-run the tests:

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~JsonRoundTripTests"
```

Expected: all 5 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Sigil.Core/Identity/ tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs
git commit -m "feat(core): ensure identity types and protocol records round-trip through System.Text.Json"
```

---

## Task 12: Update blueprint snippets

Align the blueprint's code blocks with the landed code. Prose is untouched.

**Files:**
- Modify: `.bob/docs/sigil-architecture-blueprint.md`

- [ ] **Step 1: Update §3 `IContextStore` snippet**

Find the `IContextStore` code block in §3 (around line 696). Replace the entire block with:

```csharp
public interface IContextStore
{
    /// Returns the full snapshot + its current ETag
    Task<Result<(ContextSnapshot Snapshot, ETag ETag)>> GetSnapshotAsync(
        JobId jobId, CancellationToken ct = default);

    /// Commits a delta atomically. Fails with "etag-mismatch" if ETag changed.
    Task<Result> CommitDeltaAsync(
        JobId jobId, ContextDelta delta, ETag expectedETag, CancellationToken ct = default);

    /// Append to the interaction log
    Task AppendLogAsync(JobId jobId, AgentLogEntry entry, CancellationToken ct = default);

    /// Get full interaction history
    Task<IReadOnlyList<AgentLogEntry>> GetLogAsync(JobId jobId, CancellationToken ct = default);
}

public record ContextSnapshot
{
    public JobId JobId { get; init; }
    public IReadOnlyDictionary<string, object> State { get; init; } = new Dictionary<string, object>();

    public Maybe<T> Get<T>(string key) =>
        State.TryGetValue(key, out var val) && val is T typed ? Maybe.From(typed) : Maybe<T>.None;
}

public record ContextDelta
{
    public Dictionary<string, object> Updates { get; init; } = new();
    public string[] Removals { get; init; } = [];
}
```

- [ ] **Step 2: Update §3 `IAgentRegistrationStore` snippet**

Find the `IAgentRegistrationStore` code block in §3 (around line 315). Replace with:

```csharp
public interface IAgentRegistrationStore
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);
    Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);
    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
```

- [ ] **Step 3: Update §3 `AgentRegistration` snippet**

Change `public string AgentId { get; init; } = default!;` to `public AgentId AgentId { get; init; }`. Leave the rest of the record unchanged.

- [ ] **Step 4: Update §4.6 `IAuditStore` snippet**

Find the `IAuditStore` block (around line 901). Replace with:

```csharp
public interface IAuditStore
{
    Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(JobId jobId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(AgentId agentId, CancellationToken ct = default);
}

public record AuditEntry
{
    public string AuditId { get; init; } = Guid.NewGuid().ToString("N");
    public JobId JobId { get; init; }
    public AgentId AgentId { get; init; }
    public StepId StepId { get; init; }
    public ContextDelta Delta { get; init; } = new();
    public UsageMetrics Metrics { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 5: Verify the markdown is valid** (skim-read the file, look for obvious breakage)

```bash
dotnet build sigil.sln
```

Expected: still builds (this is a doc-only change, but defensively confirm).

- [ ] **Step 6: Commit**

```bash
git add .bob/docs/sigil-architecture-blueprint.md
git commit -m "docs(blueprint): align §3/§4.6 code snippets with Sigil.Core landed types"
```

---

## Task 13: Final verification — full build, full test, format check

**Files:** none modified.

- [ ] **Step 1: Clean build**

```bash
dotnet build sigil.sln --no-incremental
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 2: Full test run**

```bash
dotnet test sigil.sln
```

Expected: all tests pass (~25+ tests across Identity, Protocol, Audit, JsonRoundTrip). Report count in PR description.

- [ ] **Step 3: Format check**

```bash
dotnet format sigil.sln --verify-no-changes
```

Expected: exits with 0, no formatting changes required.

- [ ] **Step 4: Coverage sanity check**

```bash
dotnet test sigil.sln /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

Expected: coverage for `Sigil.Core` assembly >80%. (The contract layer is small; this should be easily met.)

- [ ] **Step 5: Push branch, open PR**

```bash
git push -u origin feat/core-storage-contracts
gh pr create --title "feat(core): land storage contracts and identity types (#2)" --body "$(cat <<'EOF'
## Summary

Implements Issue #2 (Phase 1 · Core abstractions — ISigilStore + IAuditStore) against the design spec at `docs/superpowers/specs/2026-04-19-issue-2-core-storage-contracts-design.md`.

- `Sigil.Core` takes `CSharpFunctionalExtensions` as its single non-BCL dep. `Result` and `Maybe<T>` are the native vocabulary.
- Strongly-typed identifiers: `AgentId`, `JobId`, `StepId`, `ETag`.
- `ContextSnapshot` is immutable (`IReadOnlyDictionary<string, object>` state); `ContextDelta` stays mutable for builder ergonomics.
- Every async method on the contract layer takes `CancellationToken`.
- `UsageMetrics` and `AgentLogEntry` landed here (scope expansion from Q2 in brainstorm; shrinks #3).
- Blueprint §3 / §4.6 code snippets updated to match landed code.

Closes #2.

## Scope note for #3

Landing `UsageMetrics` and `AgentLogEntry` here removes them from #3. Updated checklist:
- [ ] `AgentExecutionPackage` (Task + Snapshot + ETag + ScopedCredentials)
- [ ] `AgentExecutionResult` (TaskId + StateUpdates + Logs + Metrics)
- [ ] `ValidationRequest` / `ValidationResult`
- [ ] JSON round-trip tests

## Test plan

- [x] `dotnet build sigil.sln` — 0 warnings, 0 errors
- [x] `dotnet test sigil.sln` — all tests pass
- [x] `dotnet format sigil.sln --verify-no-changes` — clean
- [x] `Sigil.Core` has exactly one package dep (`CSharpFunctionalExtensions`)
- [x] Blueprint snippets updated to match landed code
EOF
)"
```

Expected: PR opened. Update Issue #3 checklist as described in the PR body via a comment on #3.

- [ ] **Step 6: Post comment on Issue #3 to reflect descoped items**

```bash
gh issue comment 3 --body "Scope update from #2: \`UsageMetrics\` and \`AgentLogEntry\` are landing in \`Sigil.Core/Protocol/\` as part of #2 (store interfaces depend on them). Remaining deliverables under this issue: \`AgentExecutionPackage\`, \`AgentExecutionResult\`, \`ValidationRequest\`/\`ValidationResult\`, and the JSON round-trip tests for those types."
```

---

## Spec coverage check

| Spec section | Covered by task(s) |
|---|---|
| Folder layout | Tasks 1–10 create every file listed |
| Single `CSharpFunctionalExtensions` dep | Task 1 |
| Identity types (record structs, `ToString`, no implicit conversions) | Task 2 |
| Protocol records (`ContextSnapshot` immutable, `ContextDelta` mutable, `Maybe<T>` on `Get<T>`) | Tasks 3, 4 |
| `UsageMetrics`, `AgentLogEntry` (scope-expanded from #3) | Tasks 5, 6 |
| `AuditEntry` record (immutable, record equality) | Task 7 |
| Registry records (from blueprint §3) | Task 8 |
| Minimal `Job` / `Checkpoint` (interface-compile prerequisite) | Task 9 |
| Store interfaces (all six, `Result` + `Maybe` + `CancellationToken`) | Task 10 |
| JSON round-trip tests | Task 11 |
| Blueprint snippet updates | Task 12 |
| Build / test / format / coverage DoD | Task 13 |
| Issue #3 checklist trim | Task 13 Step 6 |
