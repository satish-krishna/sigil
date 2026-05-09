# Agent Definition Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the new Agent contracts in `Sigil.Core` (skills become the routable unit; model, tools, and skills become first-class on `AgentRegistration`) and update the architecture blueprint and README to describe the present-tense shape.

**Architecture:** TDD per record. Each new type lands as a separate green commit: tests first, observe failure, add the record, observe pass, commit. Refactors to existing records follow the same cycle. Deletions and interface renames are mechanical and verified by `dotnet build` rather than a behavior test (no implementations exist to test against). Documentation lands last.

**Tech Stack:** .NET 9 · xUnit · FluentAssertions 6.x (pinned: 7.x is commercial) · System.Text.Json · CSharpFunctionalExtensions (`Result`, `Maybe<T>`).

**Spec reference:** `docs/superpowers/specs/2026-05-09-agent-definition-refinement-design.md`

**Conventions to follow throughout:**
- All code, blueprint prose, and README prose describe the present. **Never** add `// formerly X`, `// renamed from`, "previously this was", "now changed to", or any other change-narrative comment or wording. Memory rule: *No before/after framing in code or blueprint.* Commit messages may use imperatives ("add", "drop", "rename") since they are operational labels for the diff.
- Match existing test style: xUnit `[Fact]`/`[Theory]` + FluentAssertions; one test class per record under `tests/Sigil.Core.Tests/Registry/`; round-trip tests append to `tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`.
- Records are `sealed`, use `init`-only setters, use `required` for mandatory fields (per commit `53cbc97`).
- No JSON converters needed for new POCO records (only the value-wrapping identity types in `Sigil.Core/Identity/` need converters; default System.Text.Json handles the new shapes).

---

## File Structure

**`src/Sigil.Core/Registry/`**
- Add: `Skill.cs`, `ModelSpec.cs`, `ToolBinding.cs`
- Modify: `AgentRegistration.cs`, `AgentMetadata.cs`, `SecurityProfile.cs`
- Delete: `Capability.cs`

**`src/Sigil.Core/Storage/`**
- Modify: `IAgentRegistrationStore.cs`

**`tests/Sigil.Core.Tests/Registry/`** (new folder)
- Add: `SkillTests.cs`, `ModelSpecTests.cs`, `ToolBindingTests.cs`, `AgentMetadataTests.cs`, `AgentRegistrationTests.cs`

**`tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`**
- Modify: append round-trip cases for `Skill`, `ModelSpec`, `ToolBinding`, `AgentMetadata`, `AgentRegistration`

**Docs**
- Modify: `.bob/docs/sigil-architecture-blueprint.md` (§2 mapping table, §3 protocol vocabulary, §4.1 data model + store interface, §4.2 planner snippet + LLM prompt template; insert new §4.x **Anatomy of an Agent**; add row to §10 deferred questions)
- Modify: `README.md` (Stack bullet for the agent runtime, Phase 1 checklist row for `Sigil.Agent.SDK`)

---

## Task 1: Add `Skill` record

**Files:**
- Test: Create `tests/Sigil.Core.Tests/Registry/SkillTests.cs`
- Create: `src/Sigil.Core/Registry/Skill.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Core.Tests/Registry/SkillTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class SkillTests
{
    [Fact]
    public void Defaults_AreEmpty()
    {
        var s = new Skill { Name = "summarize-pdf", Description = "x" };

        s.RequiredTools.Should().BeEmpty();
        s.EstimatedMaxTokens.Should().BeNull();
        s.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void TwoSkillsWithSameFields_AreEqual()
    {
        var a = new Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };
        var b = a with { };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TwoSkillsDifferingInOneField_AreNotEqual()
    {
        var a = new Skill { Name = "x", Description = "y" };
        var b = a with { Name = "z" };

        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.SkillTests"`

Expected: build error — `error CS0246: The type or namespace name 'Skill' could not be found`.

- [ ] **Step 3: Add the `Skill` record**

Create `src/Sigil.Core/Registry/Skill.cs`:

```csharp
namespace Sigil.Core.Registry;

public sealed record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
    public string Version { get; init; } = "1.0.0";
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.SkillTests"`

Expected: 3 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Registry/Skill.cs tests/Sigil.Core.Tests/Registry/SkillTests.cs
git commit -m "feat(core): add Skill record"
```

---

## Task 2: Add `ModelSpec` and `Sampling` records

**Files:**
- Test: Create `tests/Sigil.Core.Tests/Registry/ModelSpecTests.cs`
- Create: `src/Sigil.Core/Registry/ModelSpec.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Core.Tests/Registry/ModelSpecTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class ModelSpecTests
{
    [Fact]
    public void Sampling_Defaults_AreNull()
    {
        var s = new Sampling();

        s.Temperature.Should().BeNull();
        s.TopP.Should().BeNull();
        s.MaxOutputTokens.Should().BeNull();
    }

    [Fact]
    public void ModelSpec_Defaults_HasEmptySampling()
    {
        var m = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" };

        m.Sampling.Should().NotBeNull();
        m.Sampling.Temperature.Should().BeNull();
    }

    [Fact]
    public void TwoModelSpecsWithSameFields_AreEqual()
    {
        var a = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling { Temperature = 0.2, MaxOutputTokens = 800 }
        };
        var b = a with { };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TwoModelSpecsDifferingInSampling_AreNotEqual()
    {
        var a = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling { Temperature = 0.2 }
        };
        var b = a with { Sampling = new Sampling { Temperature = 0.7 } };

        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.ModelSpecTests"`

Expected: build error — `ModelSpec` and `Sampling` not found.

- [ ] **Step 3: Add the `ModelSpec` and `Sampling` records**

Create `src/Sigil.Core/Registry/ModelSpec.cs`:

```csharp
namespace Sigil.Core.Registry;

public sealed record ModelSpec
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public Sampling Sampling { get; init; } = new();
}

public sealed record Sampling
{
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.ModelSpecTests"`

Expected: 4 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Registry/ModelSpec.cs tests/Sigil.Core.Tests/Registry/ModelSpecTests.cs
git commit -m "feat(core): add ModelSpec and Sampling records"
```

---

## Task 3: Add `ToolBinding` record and `ToolKind` enum

**Files:**
- Test: Create `tests/Sigil.Core.Tests/Registry/ToolBindingTests.cs`
- Create: `src/Sigil.Core/Registry/ToolBinding.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Core.Tests/Registry/ToolBindingTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class ToolBindingTests
{
    [Fact]
    public void ToolKind_HasExpectedMembers()
    {
        Enum.GetValues<ToolKind>().Should().BeEquivalentTo(
            new[] { ToolKind.Mcp, ToolKind.Http, ToolKind.InProcess });
    }

    [Fact]
    public void TwoToolBindingsWithSameFields_AreEqual()
    {
        var a = new ToolBinding
        {
            Name = "get_forecast",
            Kind = ToolKind.Http,
            Description = "Fetch a 7-day forecast.",
            ParameterSchema = "{\"type\":\"object\"}"
        };
        var b = a with { };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TwoToolBindingsDifferingInKind_AreNotEqual()
    {
        var a = new ToolBinding
        {
            Name = "get_forecast",
            Kind = ToolKind.Http,
            Description = "x",
            ParameterSchema = "{}"
        };
        var b = a with { Kind = ToolKind.Mcp };

        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.ToolBindingTests"`

Expected: build error — `ToolBinding` and `ToolKind` not found.

- [ ] **Step 3: Add the `ToolBinding` record and `ToolKind` enum**

Create `src/Sigil.Core/Registry/ToolBinding.cs`:

```csharp
namespace Sigil.Core.Registry;

public sealed record ToolBinding
{
    public required string Name { get; init; }
    public required ToolKind Kind { get; init; }
    public required string Description { get; init; }
    public required string ParameterSchema { get; init; }
}

public enum ToolKind
{
    Mcp,
    Http,
    InProcess
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.ToolBindingTests"`

Expected: 3 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Registry/ToolBinding.cs tests/Sigil.Core.Tests/Registry/ToolBindingTests.cs
git commit -m "feat(core): add ToolBinding and ToolKind"
```

---

## Task 4: Tighten `SecurityProfile.AllowedTools` collection type

`AllowedTools` currently uses `string[]`. Align with the read-only collection convention used elsewhere on `AgentRegistration` (`IReadOnlyList<Capability>`, `IReadOnlyDictionary<string, string>`).

**Files:**
- Modify: `src/Sigil.Core/Registry/SecurityProfile.cs`

- [ ] **Step 1: Make the change**

Replace the entire contents of `src/Sigil.Core/Registry/SecurityProfile.cs`:

```csharp
namespace Sigil.Core.Registry;

public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}
```

- [ ] **Step 2: Build and test**

Run: `dotnet build sigil.sln` — expected: build succeeds.
Run: `dotnet test sigil.sln` — expected: all existing tests pass (no test currently references `AllowedTools`).

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Core/Registry/SecurityProfile.cs
git commit -m "refactor(core): make SecurityProfile.AllowedTools an IReadOnlyList"
```

---

## Task 5: Slim `AgentMetadata` to `Tags` only

**Files:**
- Test: Create `tests/Sigil.Core.Tests/Registry/AgentMetadataTests.cs`
- Modify: `src/Sigil.Core/Registry/AgentMetadata.cs`

- [ ] **Step 1: Write the failing tests against the new shape**

Create `tests/Sigil.Core.Tests/Registry/AgentMetadataTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class AgentMetadataTests
{
    [Fact]
    public void Default_HasEmptyTags()
    {
        var m = new AgentMetadata();

        m.Tags.Should().BeEmpty();
    }

    [Fact]
    public void TwoMetadataWithSameTags_AreEqual()
    {
        var a = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform" }
        };
        var b = a with { };

        a.Should().Be(b);
    }
}
```

- [ ] **Step 2: Run tests to verify (likely passes today — note any failures)**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.AgentMetadataTests"`

Expected: 2 passed (the current `AgentMetadata` already exposes `Tags`; the tests do not reference `Model` or `MaxTokenBudget`, so they pass against the old shape too). This is intentional — the tests pin the *new* shape; the next step removes the deprecated fields and re-running confirms nothing else broke.

- [ ] **Step 3: Slim the record**

Replace the entire contents of `src/Sigil.Core/Registry/AgentMetadata.cs`:

```csharp
namespace Sigil.Core.Registry;

public sealed record AgentMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
```

- [ ] **Step 4: Build the whole solution**

Run: `dotnet build sigil.sln`

Expected: build succeeds. (No other code references `AgentMetadata.Model` or `AgentMetadata.MaxTokenBudget` — confirmed via grep before plan was written. If this fails, stop and reconcile.)

- [ ] **Step 5: Run all tests**

Run: `dotnet test sigil.sln`

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Core/Registry/AgentMetadata.cs tests/Sigil.Core.Tests/Registry/AgentMetadataTests.cs
git commit -m "refactor(core): slim AgentMetadata to Tags only"
```

---

## Task 6: Refactor `AgentRegistration` (first-class Model, Skills, Tools, MaxTokenBudget)

This task replaces the `Capabilities` field with `Skills`, lifts `Model` and `MaxTokenBudget` to top-level fields, and adds `Tools`. Capability is still on disk after this task — it just isn't referenced from the registration anymore. (Task 7 deletes it.)

**Files:**
- Test: Create `tests/Sigil.Core.Tests/Registry/AgentRegistrationTests.cs`
- Modify: `src/Sigil.Core/Registry/AgentRegistration.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Core.Tests/Registry/AgentRegistrationTests.cs`:

```csharp
using FluentAssertions;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class AgentRegistrationTests
{
    private static AgentRegistration MakeFull() => new()
    {
        AgentId = new AgentId("weather-bot"),
        Name = "Weather Bot",
        Domain = "weather",
        EndpointUrl = "https://weather-bot.internal:8443",
        SemanticVersion = "1.0.0",
        RoutingWeight = 100,
        Status = AgentStatus.Healthy,
        Model = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling { Temperature = 0.2, MaxOutputTokens = 800 }
        },
        Skills =
        [
            new Skill
            {
                Name = "forecast-summary",
                Description = "Summarize a forecast.",
                RequiredTools = ["get_forecast"],
                EstimatedMaxTokens = 400
            }
        ],
        Tools =
        [
            new ToolBinding
            {
                Name = "get_forecast",
                Kind = ToolKind.Http,
                Description = "Fetch a 7-day forecast.",
                ParameterSchema = "{\"type\":\"object\"}"
            }
        ],
        MaxTokenBudget = 4000,
        Security = new SecurityProfile { AllowedTools = ["get_forecast"] },
        Metadata = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform" }
        },
        RegisteredAt = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
        LastHeartbeat = new DateTime(2026, 5, 9, 12, 1, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Defaults_HasEmptySkillsAndTools()
    {
        var r = new AgentRegistration
        {
            Name = "x",
            Domain = "y",
            EndpointUrl = "https://example",
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" }
        };

        r.Skills.Should().BeEmpty();
        r.Tools.Should().BeEmpty();
        r.MaxTokenBudget.Should().BeNull();
        r.RoutingWeight.Should().Be(100);
        r.Status.Should().Be(AgentStatus.Starting);
        r.SemanticVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void TwoRegistrationsWithSameFields_AreEqual()
    {
        var a = MakeFull();
        var b = a with { };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TwoRegistrationsDifferingInSkills_AreNotEqual()
    {
        var a = MakeFull();
        var b = a with { Skills = [] };

        a.Should().NotBe(b);
    }

    [Fact]
    public void TwoRegistrationsDifferingInModel_AreNotEqual()
    {
        var a = MakeFull();
        var b = a with
        {
            Model = a.Model with { Model = "gpt-4o" }
        };

        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.AgentRegistrationTests"`

Expected: build error — `AgentRegistration` does not have `Skills`, `Tools`, `Model`, or `MaxTokenBudget` as top-level members; or has `Capabilities` that the test does not set.

- [ ] **Step 3: Replace `AgentRegistration`**

Replace the entire contents of `src/Sigil.Core/Registry/AgentRegistration.cs`:

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

public sealed record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string EndpointUrl { get; init; }
    public string SemanticVersion { get; init; } = "1.0.0";
    public int RoutingWeight { get; init; } = 100;
    public AgentStatus Status { get; init; } = AgentStatus.Starting;

    public required ModelSpec Model { get; init; }
    public IReadOnlyList<Skill> Skills { get; init; } = [];
    public IReadOnlyList<ToolBinding> Tools { get; init; } = [];
    public int? MaxTokenBudget { get; init; }

    public SecurityProfile Security { get; init; } = new();
    public AgentMetadata Metadata { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Build the whole solution**

Run: `dotnet build sigil.sln`

Expected: build succeeds. `Capability.cs` still exists on disk and `IAgentRegistrationStore.FindByCapabilityAsync` still compiles (it takes a `string`, not a `Capability`), but `AgentRegistration` no longer references either. Task 7 will delete them.

- [ ] **Step 5: Run new tests**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~Registry.AgentRegistrationTests"`

Expected: 4 passed, 0 failed.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test sigil.sln`

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Sigil.Core/Registry/AgentRegistration.cs tests/Sigil.Core.Tests/Registry/AgentRegistrationTests.cs
git commit -m "feat(core): make Model, Skills, Tools first-class on AgentRegistration"
```

---

## Task 7: Drop the Capability concept (delete record + rename store method)

These two changes are one conceptual move — Capability is no longer a thing in Sigil. Land them together so the diff and commit message reflect the single intent.

**Files:**
- Delete: `src/Sigil.Core/Registry/Capability.cs`
- Modify: `src/Sigil.Core/Storage/IAgentRegistrationStore.cs`

- [ ] **Step 1: Delete `Capability.cs`**

Delete file: `src/Sigil.Core/Registry/Capability.cs`

```bash
rm src/Sigil.Core/Registry/Capability.cs
```

(On Windows PowerShell: `Remove-Item src/Sigil.Core/Registry/Capability.cs`)

- [ ] **Step 2: Rename `FindByCapabilityAsync` to `FindBySkillAsync`**

Replace the entire contents of `src/Sigil.Core/Storage/IAgentRegistrationStore.cs`:

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

    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(
        string skillName, CancellationToken ct = default);

    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(
        string domain, CancellationToken ct = default);

    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);

    Task<Result> UpdateStatusAsync(
        AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build the whole solution**

Run: `dotnet build sigil.sln`

Expected: build succeeds. (No implementations of `IAgentRegistrationStore` exist yet, so the rename has no consumers to update.)

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test sigil.sln`

Expected: all tests pass.

- [ ] **Step 5: Verify no orphan references remain**

Run (Grep tool): search the whole repo for `Capability` and `FindByCapabilityAsync` to confirm no straggler.

Expected matches: only the just-committed spec doc (`docs/superpowers/specs/2026-05-09-agent-definition-refinement-design.md`) and the not-yet-updated blueprint (`.bob/docs/sigil-architecture-blueprint.md`). Code/tests should have **zero** matches.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Core/Registry/Capability.cs src/Sigil.Core/Storage/IAgentRegistrationStore.cs
git commit -m "refactor(core): drop Capability — Skill is the routable unit"
```

(`git add` on a deleted file stages the deletion; `-A` is intentionally not used.)

---

## Task 8: Append round-trip tests for the new types

The test suite has a single `JsonRoundTripTests` class that covers identity types, protocol records, and audit. Append cases for the new registry types so any future serialization regression is caught.

**Files:**
- Modify: `tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`

- [ ] **Step 1: Append the new round-trip tests**

Add these `[Fact]` methods to the bottom of the `JsonRoundTripTests` class (just above the closing brace), preserving the existing `Options` and existing tests:

```csharp
    [Fact]
    public void Skill_RoundTrips()
    {
        var original = new Sigil.Core.Registry.Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Sigil.Core.Registry.Skill>(json, Options)!;

        back.Should().Be(original);
    }

    [Fact]
    public void ModelSpec_RoundTrips()
    {
        var original = new Sigil.Core.Registry.ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sigil.Core.Registry.Sampling
            {
                Temperature = 0.2,
                TopP = 0.9,
                MaxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Sigil.Core.Registry.ModelSpec>(json, Options)!;

        back.Should().Be(original);
    }

    [Fact]
    public void ToolBinding_RoundTrips()
    {
        var original = new Sigil.Core.Registry.ToolBinding
        {
            Name = "get_forecast",
            Kind = Sigil.Core.Registry.ToolKind.Http,
            Description = "Fetch a 7-day forecast.",
            ParameterSchema = "{\"type\":\"object\"}"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Sigil.Core.Registry.ToolBinding>(json, Options)!;

        back.Should().Be(original);
    }

    [Fact]
    public void AgentMetadata_RoundTrips()
    {
        var original = new Sigil.Core.Registry.AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform", ["tier"] = "standard" }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Sigil.Core.Registry.AgentMetadata>(json, Options)!;

        back.Tags.Should().ContainKey("team").WhoseValue.Should().Be("platform");
        back.Tags.Should().ContainKey("tier").WhoseValue.Should().Be("standard");
    }

    [Fact]
    public void AgentRegistration_RoundTrips()
    {
        var original = new Sigil.Core.Registry.AgentRegistration
        {
            AgentId = new AgentId("weather-bot"),
            Name = "Weather Bot",
            Domain = "weather",
            EndpointUrl = "https://weather-bot.internal:8443",
            SemanticVersion = "1.0.0",
            RoutingWeight = 100,
            Status = Sigil.Core.Registry.AgentStatus.Healthy,
            Model = new Sigil.Core.Registry.ModelSpec
            {
                Provider = "openai",
                Model = "gpt-4o-mini",
                Sampling = new Sigil.Core.Registry.Sampling { Temperature = 0.2, MaxOutputTokens = 800 }
            },
            Skills =
            [
                new Sigil.Core.Registry.Skill
                {
                    Name = "forecast-summary",
                    Description = "Summarize a forecast.",
                    RequiredTools = new[] { "get_forecast" },
                    EstimatedMaxTokens = 400
                }
            ],
            Tools =
            [
                new Sigil.Core.Registry.ToolBinding
                {
                    Name = "get_forecast",
                    Kind = Sigil.Core.Registry.ToolKind.Http,
                    Description = "Fetch a 7-day forecast.",
                    ParameterSchema = "{\"type\":\"object\"}"
                }
            ],
            MaxTokenBudget = 4000,
            Security = new Sigil.Core.Registry.SecurityProfile { AllowedTools = new[] { "get_forecast" } },
            Metadata = new Sigil.Core.Registry.AgentMetadata
            {
                Tags = new Dictionary<string, string> { ["team"] = "platform" }
            },
            RegisteredAt = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            LastHeartbeat = new DateTime(2026, 5, 9, 12, 1, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Sigil.Core.Registry.AgentRegistration>(json, Options)!;

        back.Name.Should().Be("Weather Bot");
        back.Skills.Should().HaveCount(1);
        back.Skills[0].Name.Should().Be("forecast-summary");
        back.Tools.Should().HaveCount(1);
        back.Tools[0].Kind.Should().Be(Sigil.Core.Registry.ToolKind.Http);
        back.Model.Provider.Should().Be("openai");
        back.MaxTokenBudget.Should().Be(4000);
        back.Security.AllowedTools.Should().Equal("get_forecast");
        back.Metadata.Tags.Should().ContainKey("team");
    }
```

(The fully-qualified type names avoid adding a `using Sigil.Core.Registry;` to the file's existing import block — minimal-diff style. If you prefer to add the using and shorten the names, that is fine too.)

- [ ] **Step 2: Run round-trip tests**

Run: `dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~JsonRoundTripTests"`

Expected: all original tests + 5 new tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs
git commit -m "test(core): round-trip Skill, ModelSpec, ToolBinding, AgentMetadata, AgentRegistration"
```

---

## Task 9: Update the architecture blueprint

Five edits inside `.bob/docs/sigil-architecture-blueprint.md`. All edits describe the present — no narrating the change.

**File:** Modify `.bob/docs/sigil-architecture-blueprint.md`

- [ ] **Step 1: §2 — analogy table row for "Devices & Drivers"**

Locate the row reading:

```
| Devices & Drivers | Tools / APIs / MCP Servers |
```

Replace with:

```
| Devices & Drivers | Tools (HTTP, MCP, in-process) and skills loaded by the agent runtime |
```

- [ ] **Step 2: §3 — Agent Protocol vocabulary**

In the Mermaid sequence diagram block under "## 3. The Agent Protocol", any narrative phrasing referring to "capability" should read "skill". Specifically, the line:

```
Gateway->>Agent: POST /sigil/validate (task preview)
```

leaves the diagram alone, but the surrounding prose paragraph and the `AgentExecutionPackage` / `AgentTask` C# example needs `CapabilityName` replaced with `SkillName`. Update the C# code block under §3 so `AgentTask` reads:

```csharp
public record AgentTask
{
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public required string SkillName { get; init; }
    public string Input { get; init; } = "";
    public IReadOnlyList<string> AvailableTools { get; init; } = [];
}
```

(If `AgentTask` is not currently spelled out in §3, this clarifies it. The point is: anywhere the protocol vocabulary mentions "capability" in this section, it now reads "skill".)

- [ ] **Step 3: §4.1 — Secure Agent Registry data model**

Replace the entire `## 4.1 Secure Agent Registry` data-model code block (the one containing `AgentRegistration`, `Capability`, `SecurityProfile`, `AgentStatus`, `AgentMetadata`) with:

```csharp
public record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string EndpointUrl { get; init; }
    public string SemanticVersion { get; init; } = "1.0.0";
    public int RoutingWeight { get; init; } = 100;          // 0–100, canary builds
    public AgentStatus Status { get; init; } = AgentStatus.Starting;

    public required ModelSpec Model { get; init; }
    public IReadOnlyList<Skill> Skills { get; init; } = [];
    public IReadOnlyList<ToolBinding> Tools { get; init; } = [];
    public int? MaxTokenBudget { get; init; }

    public SecurityProfile Security { get; init; } = new();
    public AgentMetadata Metadata { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
}

public record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
    public string Version { get; init; } = "1.0.0";
}

public record ModelSpec
{
    public required string Provider { get; init; }   // "openai" | "azure-openai" | "anthropic" | "ollama" | ...
    public required string Model { get; init; }      // "gpt-4o-mini"
    public Sampling Sampling { get; init; } = new();
}

public record Sampling
{
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
}

public record ToolBinding
{
    public required string Name { get; init; }
    public required ToolKind Kind { get; init; }       // Mcp | Http | InProcess
    public required string Description { get; init; }
    public required string ParameterSchema { get; init; }   // raw JSON-schema text
}

public enum ToolKind { Mcp, Http, InProcess }

public record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];   // names from ToolBinding
}

public enum AgentStatus
{
    Starting,
    Healthy,
    Degraded,
    Offline,
    Draining
}

public record AgentMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
```

Also under §4.1, replace the `IAgentRegistrationStore` block with:

```csharp
public interface IAgentRegistrationStore
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);
    Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);
    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
```

- [ ] **Step 4: §4.2 — Planner candidate filter and prompt template**

Locate the candidate-filter snippet under §4.2 (currently filters by `Capabilities.Any(c => c.Name == ...)`). Replace with:

```csharp
candidates.Where(a => a.Skills.Any(s => s.Name == intent.SkillName));
```

Locate any LLM planner prompt template that renders "Available Agents" with `RequiredTools` per capability. Update it so each agent renders its `Skills`, with each skill's `Name`, `Description`, `RequiredTools`, and `EstimatedMaxTokens`. The plan-step JSON schema the LLM produces references `skill_name` instead of `capability`. The exact prompt template lives in the existing §4.2 — preserve structure, swap vocabulary.

Also update the `PlanStep` record example (if present in §4.2) so the field is `SkillName` not `Capability`.

- [ ] **Step 5: Insert new §4.x — "Anatomy of an Agent"**

Add a new subsection in §4, slotted just after §4.1 (renumber subsequent subsections accordingly). Use this content verbatim:

````markdown
### 4.x Anatomy of an Agent

A Sigil agent is a configured instance of the SDK runtime. The four pillars are:

1. **The SDK** (`Sigil.Agent.SDK`) — the universal agent runtime. Owns the `/sigil/*` protocol surface, snapshot ingestion, system-prompt composition, model invocation via `IChatClient`, delta production, and lifecycle-hook dispatch.
2. **A model spec** — provider + model + sampling parameters. The agent constructs its own `IChatClient`; the kernel uses the spec for tier policy and trace tagging.
3. **A system prompt** — a base prompt plus dynamically composed skill bodies and an auto-rendered tool catalog. The SDK builds the effective prompt per execution step.
4. **Tools and skills** — the agent's verbs and packaged know-how. Tools are external (MCP servers, HTTP endpoints) or in-process C# delegates. Skills are Claude-style first-class behaviors that the planner routes to.

Hosting is **strict 1:1**: one container, one agent identity. Skill content lives inside the agent container; there is no kernel-curated skill catalog.

#### Container layout

An agent container holds three artifacts:

- `agent.json` — the static manifest (identity, model spec, tool connections, skill index).
- `skills/*.md` — Claude-style markdown files with YAML frontmatter parsed into the `Skill` record; the body composes into the system prompt at execution time.
- `Program.cs` — the SDK bootstrap, plus optional in-process tool delegates and lifecycle hooks.

Connection details (HTTP base URL, auth tokens, MCP server addresses) stay agent-side. `ToolBinding` is what the kernel sees; secrets never cross the wire.

#### Execution-step composition

When `/sigil/execute` arrives, the SDK runs:

1. Validate ETag, fetch the step. The step references a skill by `SkillName`.
2. Hook: `OnSnapshotReceived(snapshot)`.
3. Compose the system prompt: base prompt body + active skill body + auto-rendered tool catalog scoped to `skill.RequiredTools ∩ Security.AllowedTools`.
4. Hook: `OnBeforeModelCall(messages, tools)`.
5. Invoke `IChatClient` with the composed messages and function-call tools.
6. Hook: `OnAfterModelCall(modelResponse)`.
7. Drive the tool-call loop until terminal.
8. Convert the terminal response into a `ContextDelta`.
9. Hook: `OnDeltaProduced(delta)`.
10. Return `AgentExecutionResult(delta, logs, metrics)`.

The model sees only tools the active skill needs **and** that the agent's `Security.AllowedTools` permits — narrowing both attack surface and prompt size per step.

#### Lifecycle hooks

Four named optional hooks let the agent author plug in custom behavior without owning the pipeline:

- `OnSnapshotReceived(ctx, snapshot)` — transform inbound state.
- `OnBeforeModelCall(ctx, messages, tools)` — mutate prompt or tool list.
- `OnAfterModelCall(ctx, response)` — inspect raw model output.
- `OnDeltaProduced(ctx, delta)` — filter or tag outbound state.

A hook that throws aborts the step; the failed hook's name appears in `AgentExecutionResult.Logs`.
````

- [ ] **Step 6: §10 — add deferred question**

Append a row to §10 (Open Questions) reading:

```
- **Kernel-curated skill catalog.** Skills are agent-bundled today. A future kernel-curated catalog (`ISkillStore`) would enable cross-agent reuse, central governance, and skill versioning across the fleet. Surfaced when the SDK runtime lands.
```

- [ ] **Step 7: Build, test, eyeball**

Run: `dotnet build sigil.sln && dotnet test sigil.sln`

Expected: all green (no code changed in this task — sanity check).

Open the blueprint and grep it for `Capability`. Expected matches: zero. If any survive, update the prose to read "skill" or rewrite the affected sentence.

- [ ] **Step 8: Commit**

```bash
git add .bob/docs/sigil-architecture-blueprint.md
git commit -m "docs(blueprint): align Agent definition with the SDK + model + prompt + tools + skills shape"
```

---

## Task 10: Update the README

Two small edits in `README.md`.

**File:** Modify `README.md`

- [ ] **Step 1: Stack bullet for the agent runtime**

In the "## Stack" list, the line currently reads:

```
- **Agent runtime** — Microsoft Agent Framework (GA 1.0) inside each remote container
```

Replace with:

```
- **Agent runtime** — `Sigil.Agent.SDK` configured by JSON manifest + `skills/*.md` + optional C# hooks/in-process tools, riding on Microsoft Agent Framework (GA 1.0) inside each remote container
```

- [ ] **Step 2: Phase 1 checklist row**

In the "## Status" Phase 1 checklist, the line currently reads:

```
- [ ] `Sigil.Agent.SDK` (registration, heartbeat, snapshot/delta)
```

Replace with:

```
- [ ] `Sigil.Agent.SDK` (manifest loader, skill composition, lifecycle hooks, snapshot/delta)
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs(readme): describe the agent runtime as configured SDK + manifest + skills"
```

---

## Task 11: Final verification

- [ ] **Step 1: Full build + test**

Run: `dotnet build sigil.sln && dotnet test sigil.sln`

Expected: build succeeds, all tests pass (totals will include the 5 new round-trip tests + 16-ish new per-record tests across Skill/ModelSpec/ToolBinding/AgentMetadata/AgentRegistration).

- [ ] **Step 2: Confirm no `Capability` orphans in code**

Use the Grep tool: search the repo for `Capability` (case-sensitive). Expected matches:
- `docs/superpowers/specs/2026-05-09-agent-definition-refinement-design.md` (spec — describes the change, allowed)
- `docs/superpowers/plans/2026-05-09-agent-definition-refinement.md` (this plan)
- Possibly historical `.bob/plans/` files (untouched)

Expected **zero** matches in `src/`, `tests/`, `.bob/docs/sigil-architecture-blueprint.md`, or `README.md`.

If any survive in those locations, fix and amend the relevant commit (or add a follow-up commit — the project rule is to prefer new commits over amending).

- [ ] **Step 3: Confirm no `FindByCapabilityAsync` orphans**

Same check, search for `FindByCapabilityAsync`. Expected matches: only in spec and plan (allowed). Code/tests/blueprint: zero.

- [ ] **Step 4: Push the branch**

Verify the branch is `feat/skills-design` (already created when the spec was committed).

```bash
git push -u origin feat/skills-design
```

(Skip this step if a different branching convention is in play — the user may want to land via a different branch name.)

---

## Out of scope (follow-on issues)

These do **not** land in this plan; they are reserved for follow-on issues:

- `Sigil.Agent.SDK` runtime — manifest loader, skill markdown parser, system-prompt composer, lifecycle-hook dispatcher, in-process tool registration, MCP/HTTP tool invocation.
- `Sigil.Api` protocol endpoints (`/sigil/info`, `/sigil/validate`, `/sigil/execute`, `/sigil/heartbeat`).
- `Sigil.Runtime` planner update consuming `FindBySkillAsync`, `PlanStep.SkillName`, `LlmPlanner` prompt template.
- Storage provider implementations (`Sigil.Storage.Mongo`, `Sigil.Storage.EfCore`) for `AgentRegistration` reads/writes.
- Registration-time validation rules listed in spec §2.8 (live in store implementations, not the interface).
