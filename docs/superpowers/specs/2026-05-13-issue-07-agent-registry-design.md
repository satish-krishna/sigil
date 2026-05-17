# Issue #7 — Secure Agent Registry with weighted routing

**Status:** Approved — ready for implementation planning
**Date:** 2026-05-13
**Branch:** `feat/agent-registry`
**Issue:** [#7 — Phase 1 · Secure Agent Registry with weighted routing](https://github.com/satish-krishna/sigil/issues/7)
**Blueprint references:** `.bob/docs/sigil-architecture-blueprint.md` §4.1
**Depends on:** #2 (`IAgentRegistrationStore`), #4 (Sigil-Key validation surfaces `SecurityProfile`), PR #19 (registration record shape)

> The kernel's runtime façade over `IAgentRegistrationStore`. Enforces `AgentStatus` lifecycle, exposes a semantic transition API, and provides weighted random selection for canary-style routing.

---

## 1. Goal

Land `IAgentRegistry` (contract) and `AgentRegistry` (implementation) so the orchestrator (future) and lifecycle endpoints (#13) can:

- Register agents and enforce **valid status transitions only**.
- Update heartbeat with status-aware promotion (Starting/Degraded → Healthy).
- Select a single healthy agent for a skill, **weighted by `RoutingWeight`**.

The store stays a dumb persistence boundary; the registry owns the business rules.

## 2. Scope

### In scope

- `Sigil.Core/Registry/IAgentRegistry.cs` — interface (zero new deps beyond what `Sigil.Core` already references).
- `Sigil.Core/Registry/RegistryErrors.cs` — stable string error codes.
- `Sigil.Core/Registry/IRandomProvider.cs` — `int Next(int maxExclusive)` abstraction for deterministic tests.
- `Sigil.Runtime/Registry/AgentRegistry.cs` — implementation.
- `Sigil.Runtime/Registry/SystemRandomProvider.cs` — `Random.Shared`-backed adapter.
- `Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs` — `AddSigilRuntime()` DI extension.
- New test project `tests/Sigil.Runtime.Tests/` (added to `sigil.sln`).

### Out of scope (deferred)

- **Heartbeat-driven sweeps** (Degraded after 1 missed beat, Offline after 3) — Agent Health Monitor (#11). The registry exposes the *transitions*; the monitor *decides when*.
- **Stale cleanup / physical row delete** — also #11. `DeregisterAsync` is **not** added in this issue. Graceful shutdown: `BeginDrainingAsync` → `MarkOfflineAsync`. Row stays until sweep.
- **HTTP endpoints** — `POST /api/agents/register`, heartbeat, deregister, list, intent all live in #13.
- **JWT issuance, tier dispatch, mTLS** — Phase 3.
- **Multi-replica caching** — Phase 1 is single-replica.
- **Recycle of `Offline` agents via re-registration** — production `IAgentRegistrationStore` rejects duplicate `AgentId`. Until a recycle path is added (likely with #11's stale-cleanup), an `Offline` agent must be physically removed before its ID can be re-used.

## 3. Design decisions

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| Q1 | Wrap or replace the store? | **Wrap.** `AgentRegistry` takes `IAgentRegistrationStore` via DI. | Store stays the persistence boundary. Registry adds rules. Matches gateway pattern in #10. |
| Q2 | Where do transition rules live? | **In `AgentRegistry`.** Store's `UpdateStatusAsync` remains low-level. | Store has no concept of "valid transition"; that's domain logic. |
| Q3 | `DeregisterAsync` in this issue? | **No.** Defer to #11 (stale cleanup) and #13 (endpoint). | Keeps issue tight. Drained agents sit at `Offline` until sweep. |
| Q4 | Heartbeat on `Offline` agents? | **Reject** with `invalid-status-transition`. | Blueprint state diagram routes `Offline → Starting` via explicit re-register. Auto-promotion would mask split-brain. |
| Q5 | Heartbeat on `Draining` agents? | **Accept** — refresh `LastHeartbeat`, keep `Draining`. | Drain windows can be long; the agent is still alive. The monitor uses this to detect a hung drain. |
| Q6 | Weighted selection filter | **`Status == Healthy` AND `RoutingWeight > 0`.** | `Degraded` is "still up but not fully trusted" — orchestrator should retry instead. Weight `0` is the explicit "drain me out of routing" knob. |
| Q7 | Random source | **`IRandomProvider`** with `SystemRandomProvider` as default. | Lets us test the distribution deterministically without flaky 90/10 checks. |
| Q8 | Selection ordering | **Deterministic by `AgentId.ToString()`.** | Reproducible tests; weighted-pick algorithm is order-sensitive. |
| Q9 | Where do tests live? | **New `tests/Sigil.Runtime.Tests/` project.** | Mirrors the production project split. Registry implementation belongs with Runtime. |
| Q10 | Concurrency for transitions | **None added (best-effort).** | Store is single-row; #5/#6 already handle ETag where it matters. Status races between health monitor and registration endpoint resolve to "last writer wins" — acceptable for Phase 1. |

## 4. Interface

```csharp
namespace Sigil.Core.Registry;

public interface IAgentRegistry
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> GetAsync(AgentId id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);

    Task<Result> HeartbeatAsync(AgentId id, CancellationToken ct = default);
    Task<Result> MarkHealthyAsync(AgentId id, CancellationToken ct = default);
    Task<Result> MarkDegradedAsync(AgentId id, CancellationToken ct = default);
    Task<Result> MarkOfflineAsync(AgentId id, CancellationToken ct = default);
    Task<Result> BeginDrainingAsync(AgentId id, CancellationToken ct = default);

    Task<Maybe<AgentRegistration>> SelectByWeightAsync(string skillName, CancellationToken ct = default);
}
```

### Error codes (`RegistryErrors`)

| Constant | Value | When |
|---|---|---|
| `AgentNotFound` | `"agent-not-found"` | `Get`/transition on unknown `AgentId` |
| `InvalidStatusTransition` | `"invalid-status-transition"` | Transition not allowed by matrix below |
| `InvalidRoutingWeight` | `"invalid-routing-weight"` | `RegisterAsync` with `RoutingWeight < 0` or `> 100` |

## 5. Status transition matrix

Registry enforces only these transitions. All others return `Result.Failure(RegistryErrors.InvalidStatusTransition)`.

| From \ To  | Healthy | Degraded | Offline | Draining |
|------------|---------|----------|---------|----------|
| Starting   |   ✅    |    —     |   ✅    |    —     |
| Healthy    |   —     |   ✅     |   ✅    |   ✅     |
| Degraded   |   ✅    |    —     |   ✅    |   ✅     |
| Offline    |   —     |   —      |   —     |   —      |
| Draining   |   —     |   —      |   ✅    |   —      |

`RegisterAsync` always inserts/updates with `Status = Starting`.

`HeartbeatAsync` behavior:

| Current status | Result |
|---|---|
| Starting | `LastHeartbeat` updated **and** status → `Healthy` |
| Healthy | `LastHeartbeat` updated, status unchanged |
| Degraded | `LastHeartbeat` updated **and** status → `Healthy` |
| Draining | `LastHeartbeat` updated, status unchanged |
| Offline | rejected — `invalid-status-transition` |

## 6. Weighted selection algorithm

```text
candidates ← FindBySkillAsync(skill)
            .Where(a => a.Status == Healthy && a.RoutingWeight > 0)
            .OrderBy(a => a.AgentId.ToString())

if candidates is empty → Maybe.None

total ← Σ candidate.RoutingWeight
roll  ← random.Next(total)              // 0 .. total-1
running ← 0
for each c in candidates:
    running += c.RoutingWeight
    if roll < running → return Maybe.From(c)
```

The deterministic order makes tests reproducible. Weight `0` is the documented opt-out (used by `Draining`-adjacent flows and ops-driven cordoning later).

## 7. Tests

### `tests/Sigil.Runtime.Tests/Registry/AgentRegistryTests.cs`

A minimal in-memory `FakeAgentRegistrationStore` (added in the same test project) backs all tests. No mocking framework.

**Registration**
- `RegisterAsync` persists with `Status = Starting`.
- `RegisterAsync` with `RoutingWeight = -1` or `101` → `invalid-routing-weight`.
- Re-registration of an `Offline` agent resets to `Starting`.

**Heartbeat**
- From `Starting` → status becomes `Healthy`, `LastHeartbeat` advances.
- From `Healthy` → status unchanged, `LastHeartbeat` advances.
- From `Degraded` → status becomes `Healthy`.
- From `Draining` → status stays `Draining`, `LastHeartbeat` advances.
- From `Offline` → `invalid-status-transition`.
- Unknown `AgentId` → `agent-not-found`.

**Transitions (one test per matrix cell)**
- `Mark{Healthy|Degraded|Offline}Async` and `BeginDrainingAsync` against every starting status — assert legal vs. illegal per §5.

**Weighted selection**
- Empty pool (no agents with the skill) → `Maybe.None`.
- All candidates non-`Healthy` → `Maybe.None`.
- All candidates have `RoutingWeight = 0` → `Maybe.None`.
- Single `Healthy` candidate → always selected.
- Two `Healthy` candidates (w=10 vs w=90) with a seeded `IRandomProvider` → over 10 000 draws, distribution is 90/10 within ±2%. Use a seeded `Random` (e.g., seed 42) wrapped in a test `IRandomProvider`; assertion uses tolerance, not exact count.
- `SkillName` null/whitespace → throws `ArgumentException` (programmer error, not a domain-level failure).

### `tests/Sigil.Core.Tests/Registry/RegistryErrorsTests.cs`

Trivial — assert error constants exist and are non-empty strings. (Guards against rename without intent.)

## 8. DI wiring

```csharp
// Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs
public static IServiceCollection AddSigilRuntime(this IServiceCollection services)
{
    services.TryAddSingleton<IRandomProvider, SystemRandomProvider>();
    services.AddScoped<IAgentRegistry, AgentRegistry>();
    return services;
}
```

`Sigil.Api/Program.cs` is **not** modified in this issue. The composition root will pick this up when #13 lands.

## 9. Verification

- `dotnet build sigil.sln` clean (no new warnings).
- `dotnet test sigil.sln` green.
- New test project added to `sigil.sln`.
- `Sigil.Core` retains zero dependencies on storage providers, HTTP, or LLM clients.

## 10. Open questions

None. All resolved during brainstorming.
