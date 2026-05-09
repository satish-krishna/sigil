# Issue #3 — Agent Protocol Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the five wire-level protocol records that cross between kernel and agent, plus their tests, in `Sigil.Core/Protocol`. After this PR, the gateway (#10) and SDK (#8) workstreams have a stable contract surface to build against.

**Architecture:** Pure contract layer. Sealed records, immutable, with strong identity types from `Sigil.Core/Identity` (`JobId`, `StepId`, `ETag`) and the snapshot/delta types already shipped (`ContextSnapshot`, `ContextDelta`, `AgentLogEntry`, `UsageMetrics`). Records that hold collections override `Equals` / `GetHashCode` for value equality, matching the pattern landed alongside PR #19 in `Skill`, `SecurityProfile`, `AgentMetadata`.

**Tech Stack:** .NET 9, C# 13, `System.Text.Json` (no external serializer), Shouldly for tests.

**Branch:** `feat/agent-protocol-types`

**Issue:** [#3](https://github.com/satish-krishna/sigil/issues/3)

**Sources of truth:**
- Design spec: `docs/superpowers/specs/2026-05-09-agent-definition-refinement-design.md` §5 (post-PR-#19, authoritative)
- Blueprint: `.bob/docs/sigil-architecture-blueprint.md` §3 (predates PR #19, **shapes need a doc-sync** as part of this PR)

---

## Design call: spec wins over blueprint §3

Blueprint §3 and design spec §5 show different shapes. The blueprint predates the Skill refactor and uses primitives (`Dictionary<string, object>` for snapshot, `string` for ETag); the spec post-dates it and uses strong identity types. The issue body copies spec deliverable language verbatim.

This PR commits to the **spec shape** and updates blueprint §3 in the same commit so docs describe the present.

Specific deltas from blueprint:
- `AgentExecutionPackage.ContextSnapshot: Dictionary<string, object>` → `Snapshot: ContextSnapshot` (strong type)
- `AgentExecutionPackage.ETag: string` → `ExpectedETag: ETag` (strong type)
- `AgentExecutionPackage.ScopedCredentials` → **dropped**. The credential-injection concern is referenced in §4.6 but is a runtime/policy concern, not a wire field. Re-add when a workstream actually needs it; speculative fields rot.
- `AgentExecutionResult` shape simplified to `Delta + Logs + Metrics`. The blueprint's `TaskId / Success / StateUpdates / Error` fields collapse: TaskId lives in the inbound `AgentTask`, success/error are signaled at the kernel boundary via `Result<>` over HTTP exceptions, state updates *are* the `Delta`. The spec calls this out explicitly.
- `ValidationRequest.AvailableTools: string[]` → **dropped from the request type** because `AgentTask.AvailableTools` already carries it. No duplication.
- `ValidationResult.EstimatedTokens` becomes `int?` (nullable) per spec — the agent may not be able to estimate.

---

## File Structure

### Created

```
src/Sigil.Core/Protocol/
├── AgentTask.cs
├── AgentExecutionPackage.cs
├── AgentExecutionResult.cs
├── ValidationRequest.cs
└── ValidationResult.cs

tests/Sigil.Core.Tests/Protocol/
├── AgentTaskTests.cs
├── AgentExecutionPackageTests.cs
├── AgentExecutionResultTests.cs
├── ValidationRequestTests.cs
└── ValidationResultTests.cs

docs/superpowers/plans/2026-05-09-issue-3-agent-protocol-types.md   # this file
```

### Modified

```
tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs   # +5 round-trip tests
.bob/docs/sigil-architecture-blueprint.md               # §3.2 / §3.3 code blocks synced to landed shapes
Roadmap.md                                              # Layer 1 row #18 flipped to ✅ (already part of merge of PR #23 — verify)
```

### File responsibilities — one record per file

- **`AgentTask`** — the unit of work the kernel hands to an agent. Identifies the job/step, names the skill, carries input + the tool subset the agent is allowed to call.
- **`AgentExecutionPackage`** — wraps `AgentTask` with the immutable `ContextSnapshot` and the `ExpectedETag` for optimistic-concurrency commit.
- **`AgentExecutionResult`** — the agent's reply: a `ContextDelta`, a log buffer, and usage metrics.
- **`ValidationRequest`** — pre-flight payload to `/sigil/validate`.
- **`ValidationResult`** — pre-flight reply (`CanHandle`, optional estimate, missing tools, optional reason).

---

## Record shapes (canonical)

### `AgentTask`

```csharp
namespace Sigil.Core.Protocol;

using Sigil.Core.Identity;

public sealed record AgentTask
{
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public required string SkillName { get; init; }
    public string Input { get; init; } = "";
    public IReadOnlyList<string> AvailableTools { get; init; } = [];

    public bool Equals(AgentTask? other) { /* SequenceEqual on AvailableTools */ }
    public override int GetHashCode() { /* HashCode.Combine + per-element add */ }
}
```

### `AgentExecutionPackage`

```csharp
public sealed record AgentExecutionPackage
{
    public required AgentTask Task { get; init; }
    public required ContextSnapshot Snapshot { get; init; }
    public required ETag ExpectedETag { get; init; }
}
```

No collection field — the default record equality is fine. (`ContextSnapshot` is a record holding a defensive-copied dictionary; its own equality is its own concern, not handled here.)

### `AgentExecutionResult`

```csharp
public sealed record AgentExecutionResult
{
    public required ContextDelta Delta { get; init; }
    public IReadOnlyList<AgentLogEntry> Logs { get; init; } = [];
    public UsageMetrics Metrics { get; init; } = new();

    public bool Equals(AgentExecutionResult? other) { /* SequenceEqual on Logs */ }
    public override int GetHashCode() { /* per-element add */ }
}
```

### `ValidationRequest`

```csharp
public sealed record ValidationRequest
{
    public required AgentTask Task { get; init; }
    public int AvailableTokenBudget { get; init; }
}
```

No collection field directly (Task carries one; Task's own equality covers it).

### `ValidationResult`

```csharp
public sealed record ValidationResult
{
    public bool CanHandle { get; init; }
    public int? EstimatedTokens { get; init; }
    public IReadOnlyList<string> MissingTools { get; init; } = [];
    public string? Reason { get; init; }

    public bool Equals(ValidationResult? other) { /* SequenceEqual on MissingTools */ }
    public override int GetHashCode() { /* per-element add */ }
}
```

---

## Tasks

- [ ] **1. Create `Sigil.Core/Protocol/AgentTask.cs`** with strong identity types and value equality on `AvailableTools`.

- [ ] **2. Create `Sigil.Core/Protocol/AgentExecutionPackage.cs`** referencing `AgentTask`, `ContextSnapshot`, `ETag`.

- [ ] **3. Create `Sigil.Core/Protocol/AgentExecutionResult.cs`** with value equality on `Logs`.

- [ ] **4. Create `Sigil.Core/Protocol/ValidationRequest.cs`**.

- [ ] **5. Create `Sigil.Core/Protocol/ValidationResult.cs`** with value equality on `MissingTools`.

- [ ] **6. Per-record tests** under `tests/Sigil.Core.Tests/Protocol/`. Each suite covers:
  - default-construct shape
  - explicit init shape
  - value equality: equal-with-equal-collections, not-equal-with-different-collections, hash-code stability across two structurally equal instances
  - (where applicable) `required` member enforcement is implicit by compiler, no test needed

- [ ] **7. JSON round-trip** — append five `[Fact]` methods to `tests/Sigil.Core.Tests/Protocol/JsonRoundTripTests.cs`, one per record, populating every field with non-default values and asserting full round-trip via Shouldly.

- [ ] **8. Blueprint §3 doc-sync.** Replace the §3.2 and §3.3 C# code blocks with the landed shapes. Drop the `ScopedCredentials` reference, the `TaskId/Success/StateUpdates/Error` fields, the `string[] AvailableTools` on `ValidationRequest`, and the inline `AgentLogEntry` / `UsageMetrics` definitions (those already live in §3 prose; the records in `Sigil.Core/Protocol` are now authoritative).

- [ ] **9. Roadmap update.** Flip the Layer 1 row for #18 to ✅ if it's not already (PR #23 may have included this — verify). No graph edits.

- [ ] **10. Build clean.** `dotnet build sigil.sln` — 0 warnings, 0 errors.

- [ ] **11. Tests green.** `dotnet test sigil.sln` — all green, expected count: previous 70 + 5 round-trip + ~3-per-record × 5 ≈ ~90 tests. Exact count to be reported in the PR description, not pre-committed.

- [ ] **12. Commit and open PR** against `main`, linking #3.

---

## Acceptance (mirrors issue #3)

- `AgentTask`, `AgentExecutionPackage`, `AgentExecutionResult`, `ValidationRequest`, `ValidationResult` exist under `src/Sigil.Core/Protocol/`.
- Each has at least one per-record test file.
- `JsonRoundTripTests.cs` covers each new record.
- `dotnet build sigil.sln` is clean.
- `dotnet test sigil.sln` is green.

---

## Out of scope (follow-on issues)

- The SDK runtime that consumes these records (#20, #21, #22).
- The `Sigil.Api` endpoints that publish `/sigil/validate` and `/sigil/execute` (#13).
- The gateway client that signs and POSTs `AgentExecutionPackage` over the wire (#10).
- Any storage of these records — they are wire types, not persistent entities.
- Validation-rule logic (token-budget math, missing-tool computation). The records carry the result; the *logic* lives in the SDK and the agent author's hooks.

---

## Risk & rollback

Pure additive contract work in `Sigil.Core`. Risk profile:
- **Wire-shape regret.** Once consumers (SDK, gateway) bind to these shapes, changes are coordinated. The spec has been through review; the doc-sync to blueprint §3 makes the shape visible at the design level. If a field turns out to be wrong, follow-up issues can refine it before the SDK/gateway ship.
- **Equality-override drift.** Easy to forget `GetHashCode` when adding `Equals`. The pattern from `Skill.cs` is copy-pasteable; tests guard it.

Rollback: `git revert` of the single commit. No external systems touched.
