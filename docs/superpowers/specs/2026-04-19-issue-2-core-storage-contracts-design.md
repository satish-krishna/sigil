# Issue #2 — Core storage contracts (`Sigil.Core`)

**Status:** Approved — ready for implementation planning
**Date:** 2026-04-19
**Branch:** `feat/core-storage-contracts`
**Issue:** [#2 — Phase 1 · Core abstractions — ISigilStore + IAuditStore](https://github.com/satish-krishna/sigil/issues/2)
**Blueprint references:** `.bob/docs/sigil-architecture-blueprint.md` §3, §4.6

## Summary

Land the storage contract and immutable audit contract in `Sigil.Core`, plus the minimum protocol types the storage surface depends on. `Sigil.Core` takes a single non-BCL dependency — `CSharpFunctionalExtensions` — so that `Result` and `Maybe<T>` are the native vocabulary of the contract layer, in line with `CONTRIBUTING.md`.

Every signature uses strongly-typed identifiers (`JobId`, `AgentId`, `StepId`, `ETag`) and accepts `CancellationToken`. Snapshots are immutable once constructed; deltas remain mutable during construction.

## Decisions (brainstorm log)

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| Q1 | `CSharpFunctionalExtensions` dep vs strict BCL? | **Take the dep.** `Result` for fallible ops, `Maybe<T>` for lookups. | `CONTRIBUTING.md` makes this the house standard; `Sigil.Core` is exactly where that vocabulary should originate. "Zero deps" in the issue predates the standard. |
| Q2 | How to handle `UsageMetrics` / `AgentLogEntry` (owned by #3)? | **Expand #2** to land them in `Sigil.Core/Protocol/`. Shrink #3. | Store interfaces reference them; shipping a contract over a type that doesn't exist is wrong. #3 still owns execution/validation types. |
| Q3 | Stringly-typed ids or strong types? | **Strong types.** `readonly record struct JobId/AgentId/StepId/ETag(string Value)`. | Cheapest moment to do this is the contract layer. Zero runtime cost; real compile-time safety. |
| Q4 | State bag mutability? | **Asymmetric.** `ContextSnapshot.State` is `IReadOnlyDictionary<string, object>`; `ContextDelta.Updates` stays `Dictionary<string, object>`. | Snapshots are committed artifacts (must be immutable); deltas are builder-populated DTOs (mutable is fine). `IReadOnlyDictionary` avoids pulling `System.Collections.Immutable` into Core. |
| Q5 | `CancellationToken` on async contract methods? | **Required, last parameter, defaults to `default`.** Update blueprint snippets to match. | Non-negotiable convention for .NET async. Cheapest at the contract layer. |

## Scope

### In scope

- `Sigil.Core` — interfaces, records, identity types.
- Single package dep: `CSharpFunctionalExtensions` in `Sigil.Core.csproj`.
- `tests/Sigil.Core.Tests/` — new xUnit project, ~10–15 tests.
- `.bob/docs/sigil-architecture-blueprint.md` — update §3 and §4.6 code blocks so they match the landed code. Prose untouched.
- Trimming the checklist on [Issue #3](https://github.com/satish-krishna/sigil/issues/3) to remove `UsageMetrics` / `AgentLogEntry` (done when the PR is opened).

### Out of scope

- Concrete storage providers — Mongo (#5), EF Core (#6).
- `SigilBuilder` / `AddSigil` DI extensions — land with providers.
- `AgentRegistration`, `Job`, `Checkpoint` record bodies beyond what the blueprint's other sections already define. This PR adds the *store interfaces* over them, not the record shapes themselves.
- `AgentExecutionPackage`, `AgentExecutionResult`, `ValidationRequest`, `ValidationResult` — remain under #3.

## Design

### Folder layout

```
src/Sigil.Core/
├── Sigil.Core.csproj          # ref: CSharpFunctionalExtensions
├── Identity/
│   ├── AgentId.cs             # readonly record struct
│   ├── JobId.cs
│   ├── StepId.cs
│   └── ETag.cs
├── Protocol/
│   ├── ContextSnapshot.cs
│   ├── ContextDelta.cs
│   ├── UsageMetrics.cs        # pulled in from #3
│   └── AgentLogEntry.cs       # pulled in from #3
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
└── Sigil.Core.Tests.csproj    # xUnit; refs Sigil.Core
```

### Identity types

```csharp
public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
// JobId, StepId, ETag follow the same shape.
```

- Zero-allocation.
- `ToString()` override keeps structured logs clean (the raw string, not `AgentId { Value = ... }`).
- No implicit conversions to/from `string`. Callers construct intentionally at boundaries (deserialisers, factories).
- Record structs give value equality and `GetHashCode` for free.

### Protocol records

```csharp
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

public sealed record ContextDelta
{
    public Dictionary<string, object> Updates { get; init; } = new();
    public string[] Removals { get; init; } = [];
}

public sealed record UsageMetrics
{
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyDictionary<string, object> Custom { get; init; }
        = new Dictionary<string, object>();
}

public sealed record AgentLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AgentId AgentId { get; init; }
    public string Level { get; init; } = "Info";
    public string Message { get; init; } = "";
}
```

### Audit record

```csharp
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

Append-only at the store level. The record itself is structurally immutable via `init`-only setters; store implementations are responsible for enforcing "no updates / no deletes."

### Store interfaces

```csharp
public interface ISigilStore
{
    IAgentRegistrationStore Agents { get; }
    IJobStore Jobs { get; }
    IContextStore Contexts { get; }
    ICheckpointStore Checkpoints { get; }
    IAuditStore Audit { get; }
}

public interface IContextStore
{
    Task<Result<(ContextSnapshot Snapshot, ETag ETag)>>
        GetSnapshotAsync(JobId jobId, CancellationToken ct = default);

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

public interface IAuditStore
{
    Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(
        JobId jobId, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(
        AgentId agentId, CancellationToken ct = default);
}
```

`IAgentRegistrationStore`, `IJobStore`, and `ICheckpointStore` get the minimum surface the blueprint's documented flows require. Lookups return `Task<Maybe<T>>`; mutations return `Task<Result>`. No speculative methods — add them with the concrete providers.

### Result and Maybe semantics

| Method | Success | Failure |
|---|---|---|
| `GetSnapshotAsync` | `Result.Success((snapshot, etag))` | `Result.Failure<...>("job-not-found")` |
| `CommitDeltaAsync` | `Result.Success()` | `Result.Failure("etag-mismatch")` |
| Lookup-style methods (`FindByIdAsync` etc.) | `Maybe.From(value)` | `Maybe<T>.None` |
| Mutation-style methods | `Result.Success()` | `Result.Failure("<reason-slug>")` |

Failure strings are stable identifiers — callers (including tests and HTTP response-mappers) switch on them. No localised / user-facing messages at this layer.

### Blueprint updates

In the same PR, update `.bob/docs/sigil-architecture-blueprint.md`:

- §3 — `IContextStore` code block: `string` ids → strong types; `Task<bool>` → `Task<Result>`; `T?` → `Maybe<T>`; add `CancellationToken`; `Dictionary<string, object> State` → `IReadOnlyDictionary<string, object>`.
- §4.6 — same treatment for `ISigilStore` and `IAuditStore` blocks.

Prose stays untouched. The blueprint remains the source of architectural truth; we're correcting code snippets that drifted relative to the standards in `CONTRIBUTING.md`.

## Testing

New project: `tests/Sigil.Core.Tests/` (xUnit).

Test coverage:

- **Record equality**
  - Two `AuditEntry` instances with identical fields are equal.
  - Mutating any single field produces inequality.
  - Same for `ContextSnapshot`, `ContextDelta`, `UsageMetrics`, `AgentLogEntry`.
- **`ContextSnapshot.Get<T>`**
  - Missing key → `Maybe<T>.None`.
  - Present key, wrong type → `Maybe<T>.None`.
  - Present key, matching type → `Maybe.From(value)`.
- **Identity types**
  - `AgentId("a") == AgentId("a")`, `AgentId("a") != AgentId("b")`.
  - `ToString()` returns the raw `Value`.
  - Round-trip through `System.Text.Json` preserves value.
- **JSON round-trip (`System.Text.Json`)**
  - `ContextDelta`, `AuditEntry`, `UsageMetrics`, `AgentLogEntry` serialise and deserialise without loss. Downstream storage providers rely on this contract being stable.

No mocks, no test doubles — `Sigil.Core` has no behaviour to mock. Expect ~10–15 tests total.

## Deliverables mapped to issue checklist

| Issue checklist item | Where |
|---|---|
| `ISigilStore` aggregate + sub-stores | `src/Sigil.Core/Storage/` |
| `ContextSnapshot`, `ContextDelta` records | `src/Sigil.Core/Protocol/` |
| `AuditEntry` record (immutable) | `src/Sigil.Core/Audit/` |
| ETag-aware `IContextStore.CommitDeltaAsync` | `src/Sigil.Core/Storage/IContextStore.cs` (returns `Result`, takes strong `ETag`) |
| Unit tests for record equality / `Get<T>` | `tests/Sigil.Core.Tests/` |
| **Scope expansions (see Q2, Q3):** `UsageMetrics`, `AgentLogEntry`, identity types, blueprint-snippet update | `src/Sigil.Core/Protocol/`, `src/Sigil.Core/Identity/`, `.bob/docs/sigil-architecture-blueprint.md` |

## Definition of done (see `CONTRIBUTING.md`)

- `dotnet build sigil.sln` clean (no new warnings).
- `dotnet format sigil.sln --verify-no-changes` clean.
- `dotnet test sigil.sln` passes; `Sigil.Core.Tests` coverage >80%.
- No `null` returns; no `throw` for expected failures.
- `Sigil.Core` still has zero dependencies on storage, LLM providers, or HTTP. (`CSharpFunctionalExtensions` is the single allowed non-BCL dep.)
- Blueprint snippets match landed code.
- Issue #3 checklist updated on PR-open.
- PR title, commits, branch name follow Conventional Commits.
