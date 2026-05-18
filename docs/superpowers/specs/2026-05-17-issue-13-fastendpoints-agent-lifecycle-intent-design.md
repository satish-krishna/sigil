# Issue #13 — FastEndpoints: agent lifecycle + intent

**Issue:** [#13](https://github.com/satish-krishna/sigil/issues/13)
**Blueprint:** `.bob/docs/sigil-architecture-blueprint.md` §9 (Phase 1)
**Depends on:** #7 (Agent Registry, merged), #10 (Secure Gateway, merged), #4 (Sigil-Key validation, merged)

## Goal

Expose the kernel's first HTTP API: four agent-lifecycle endpoints (`register` / `deregister` / `heartbeat` / `list`) and a synchronous intent-dispatch endpoint. All traffic is authenticated via the Open-tier `X-Sigil-Agent-Id` + `X-Sigil-Key` headers. A thin `IIntentDispatcher` seam in `Sigil.Core` chains registry-selection → gateway-validate → gateway-execute so Phase 2 can swap in the real orchestrator without touching the endpoint.

## Non-goals (explicitly deferred)

- Job persistence / async dispatch — `/api/intents` returns the agent's result in the HTTP response. No `IJobStore` work in this issue.
- Real `IPlanner` (Deterministic / LLM / Hybrid) — Phase 2.
- Snapshot building — Phase 1 accepts a client-supplied snapshot or an empty one. `SnapshotEngine` is Phase 2.
- Standard / Trusted tiers — only Open tier is wired here (single `ISigilSecurity` call, `requiredTier: Open`).
- Storage-provider wiring is *not* added by this issue. The host (`Program.cs`) calls the EF Core provider's `AddSigilEfCoreStorage` already (added in #6).

## Architecture

```
HTTP client
  │
  ▼
SigilAuthMiddleware (new, Sigil.Api/Security/)
  │   ── reads X-Sigil-Agent-Id + X-Sigil-Key
  │   ── ISigilSecurity.AuthenticateAsync(creds, SecurityTier.Open)
  │   ── stashes AuthenticationResult on HttpContext.Items["sigil.auth"]
  ▼
FastEndpoints
  ├── Endpoints/Agents/{Register,Deregister,Heartbeat,ListAgents}Endpoint
  │       └── IAgentRegistry   (existing, Sigil.Runtime)
  │
  └── Endpoints/Intents/SubmitIntentEndpoint
          └── IIntentDispatcher (new, Sigil.Core abstraction)
                  └── SimpleIntentDispatcher (new, Sigil.Runtime)
                          ├── IAgentRegistry  (SelectByWeightAsync)
                          └── IAgentGateway   (Validate → Execute)
```

`Sigil.Core` gains no transitive dependencies (the interface is data-only). `Sigil.Runtime` already depends on `Sigil.Core`. `Sigil.Api` adds a project reference to `Sigil.Infrastructure` (for `AddSigilSecurity`).

## Authentication middleware

**File:** `src/Sigil.Api/Security/SigilAuthMiddleware.cs`

- Reads `X-Sigil-Agent-Id` and `X-Sigil-Key` from the request. Both required.
- Parses the agent id via `AgentId.Parse` (failure → `401 invalid-agent-id`).
- Builds `SigilCredentials { AgentId, SigilKey }` and calls `ISigilSecurity.AuthenticateAsync(creds, SecurityTier.Open)`.
- On success: stores the returned `AuthenticationResult` at `HttpContext.Items["sigil.auth"]` and calls `next`.
- On failure: returns HTTP `401` with JSON body `{ "error": "<code>" }` where `<code>` is the `SigilSecurityErrors.*` constant (e.g. `unknown_agent`, `key_mismatch`, `missing_key`).
- Missing headers → `401` with `{ "error": "missing-credentials" }`.
- Wired in `Program.cs` *before* `app.UseFastEndpoints()`.

The middleware is the single auth gate; endpoints do not call `ISigilSecurity` directly.

## Endpoint contracts

All endpoints serialize via FastEndpoints' default System.Text.Json configuration. The existing converter set for `AgentId` / `ETag` / `JobId` / `StepId` is registered globally in `Program.cs`.

### `POST /api/agents/register`

- **File:** `src/Sigil.Api/Endpoints/Agents/RegisterEndpoint.cs`
- **Request:** full `AgentRegistration` JSON. Server-managed fields (`Status`, `RegisteredAt`, `LastHeartbeat`) are ignored if present.
- **Caller assertion:** `HttpContext.Items["sigil.auth"].AgentId` must equal request body's `AgentId` → else `403 caller-agent-mismatch`.
- Calls `IAgentRegistry.RegisterAsync(registration)`.
- **Responses:**
  - `201 Created` with the persisted `AgentRegistration` in the body.
  - `409 Conflict` `{ "error": "<RegistryErrors.*>" }` for duplicate id or storage conflict.
  - `400 Bad Request` `{ "error": "<RegistryErrors.*>" }` for validation failures (empty skills, etc.).

### `POST /api/agents/{id}/deregister`

- **File:** `src/Sigil.Api/Endpoints/Agents/DeregisterEndpoint.cs`
- **Route id assertion:** `{id}` must equal the authenticated `AgentId` → else `403 caller-agent-mismatch`.
- Calls `IAgentRegistry.MarkOfflineAsync(id)`.
- **Responses:**
  - `204 No Content` on success.
  - `404 Not Found` `{ "error": "agent_not_found" }` when registry returns the not-found code.

### `POST /api/agents/{id}/heartbeat`

- **File:** `src/Sigil.Api/Endpoints/Agents/HeartbeatEndpoint.cs`
- Same route assertion as deregister.
- Calls `IAgentRegistry.HeartbeatAsync(id)`.
- **Responses:**
  - `204 No Content` on success.
  - `404 Not Found` for unknown agent.
  - `409 Conflict` `{ "error": "<RegistryErrors.*>" }` when the registry rejects heartbeat (e.g. agent is `Offline`).

### `GET /api/agents`

- **File:** `src/Sigil.Api/Endpoints/Agents/ListAgentsEndpoint.cs`
- **Query:** optional `?skill=<name>` or `?domain=<name>` (mutually exclusive; if both present → `400 conflicting-filters`).
- Calls `GetAllAsync` / `FindBySkillAsync` / `FindByDomainAsync` accordingly.
- **Response:** `200 OK` with `AgentRegistration[]`. No auth-id assertion — any authenticated caller may list.

### `POST /api/intents`

- **File:** `src/Sigil.Api/Endpoints/Intents/SubmitIntentEndpoint.cs`
- **Request DTO** (`Endpoints/Intents/Dtos/SubmitIntentRequest.cs`):
  ```csharp
  public sealed record SubmitIntentRequest
  {
      public required string SkillName { get; init; }
      public required string Input { get; init; }
      public ContextSnapshot? Snapshot { get; init; }
  }
  ```
- Translates the DTO to `IntentRequest` and calls `IIntentDispatcher.DispatchAsync(req)`.
- **Responses:**
  - `200 OK` with `AgentExecutionResult` in the body.
  - `404 Not Found` `{ "error": "no-agent-for-skill" }`.
  - `400 Bad Request` `{ "error": "<validation reason>" }` when the agent's `/sigil/validate` rejects.
  - `502 Bad Gateway` `{ "error": "<SigilGatewayErrors.*>" }` for gateway failures (transport, 5xx, signature).

## `IIntentDispatcher`

**File:** `src/Sigil.Core/Intents/IIntentDispatcher.cs`

```csharp
using CSharpFunctionalExtensions;
using Sigil.Core.Protocol;

namespace Sigil.Core.Intents;

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

**Error codes** (new `src/Sigil.Core/Intents/IntentErrors.cs`):
- `no-agent-for-skill` — registry's weighted selection returned `Maybe.None`.
- `validation-rejected` — fallback when the validation result has no `Reason`.

## `SimpleIntentDispatcher`

**File:** `src/Sigil.Runtime/Intents/SimpleIntentDispatcher.cs`

Constructor injects `IAgentRegistry` and `IAgentGateway`.

```csharp
public async Task<Result<AgentExecutionResult>> DispatchAsync(
    IntentRequest req, CancellationToken ct = default)
{
    var agentMaybe = await _registry.SelectByWeightAsync(req.SkillName, ct);
    if (agentMaybe.HasNoValue)
        return Result.Failure<AgentExecutionResult>(IntentErrors.NoAgentForSkill);

    var agent = agentMaybe.Value;
    var task = new AgentTask
    {
        JobId = new JobId(Guid.NewGuid().ToString()),
        SkillName = req.SkillName,
        Input = req.Input,
        AvailableTools = agent.Tools.Select(t => t.Name).ToList()
    };
    var snapshot = req.Snapshot ?? new ContextSnapshot { JobId = task.JobId };
    var valReq = new ValidationRequest { Task = task, AvailableTokenBudget = agent.MaxTokenBudget ?? 0 };

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
        ExpectedETag = new ETag("")
    };

    return await _gateway.ExecuteAsync(agent, package, ct);
}
```

`AgentTask` / `AgentExecutionPackage` / `ValidationRequest` / `ContextSnapshot.Empty` are assumed to exist in `Sigil.Core.Protocol`. The plan step that implements this will verify each field name and adjust as needed; if `ContextSnapshot.Empty` doesn't exist, the plan will add it (single-line static).

Registered in `Sigil.Runtime.DependencyInjection.SigilRuntimeServiceCollectionExtensions.AddSigilRuntime`:

```csharp
services.AddScoped<IIntentDispatcher, SimpleIntentDispatcher>();
```

## `Program.cs` wiring

```csharp
using FastEndpoints;
using Sigil.Api.Security;
using Sigil.Infrastructure.Security;
using Sigil.Runtime.DependencyInjection;
using Sigil.Storage.EfCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSigilSecurity(builder.Configuration);
builder.Services.AddSigilEfCore(builder.Configuration); // already exists from #6
builder.Services.AddSigilRuntime();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseMiddleware<SigilAuthMiddleware>();
app.UseFastEndpoints();
app.Run();

public partial class Program; // for WebApplicationFactory<Program>
```

`appsettings.Development.json` gains an `OpenTier.Keys` dictionary mapping agent ids → keys for local dev. Real keys for compose / CI come from environment variables (already supported by `SigilSecurityOptions`).

## Testing

### `tests/Sigil.Runtime.Tests/Intents/SimpleIntentDispatcherTests.cs` (unit)

Fakes for `IAgentRegistry` and `IAgentGateway`. Cases:
- `NoAgentForSkill_ReturnsFailure` — registry returns `Maybe.None` → `no-agent-for-skill`.
- `GatewayValidateFailure_PropagatesError` — gateway returns failure → same error code returned.
- `ValidationRejected_WithReason_ReturnsReason` — canHandle=false, reason="too_many_tokens" → failure with that code.
- `ValidationRejected_NoReason_ReturnsFallback` — canHandle=false, reason=null → `validation-rejected`.
- `HappyPath_CallsExecuteWithSelectedAgent` — verifies dispatcher passes the agent returned by `SelectByWeightAsync` to `ExecuteAsync` and returns the gateway's result.

### `tests/Sigil.Api.Tests/` (new project, integration)

Uses `WebApplicationFactory<Program>` with an in-memory `IAgentRegistrationStore` fake and a stub `IAgentGateway`. The project targets the same `net9.0` TFM and references `Microsoft.AspNetCore.Mvc.Testing` (to be added to `Directory.Packages.props`), `xunit`, `Shouldly` + hand-rolled fakes (matching the existing test projects — verified in the plan step).

Auth middleware suite (`Security/SigilAuthMiddlewareTests.cs`):
- Missing both headers → `401 missing-credentials`.
- Missing key only → `401 missing-credentials`.
- Unknown agent id → `401 unknown_agent`.
- Wrong key → `401 key_mismatch`.
- Malformed agent id → `401 invalid_agent_id`.
- Valid headers → request reaches endpoint (verified via list endpoint returning 200).

Per-endpoint suites (one file each under `Endpoints/Agents/` and `Endpoints/Intents/`):
- `RegisterEndpointTests` — happy 201, duplicate 409, caller-agent-mismatch 403, validation failure 400.
- `DeregisterEndpointTests` — happy 204, unknown 404, caller-agent-mismatch 403.
- `HeartbeatEndpointTests` — happy 204, unknown 404, offline 409, caller-agent-mismatch 403.
- `ListAgentsEndpointTests` — empty 200, populated 200, `?skill=` filter, `?domain=` filter, both → 400.
- `SubmitIntentEndpointTests` — happy 200, no-agent 404, validation-rejects 400, gateway-fails 502.

## Files added / changed

**New:**
- `src/Sigil.Core/Intents/IIntentDispatcher.cs`
- `src/Sigil.Core/Intents/IntentRequest.cs` (if not co-located with the interface)
- `src/Sigil.Core/Intents/IntentErrors.cs`
- `src/Sigil.Runtime/Intents/SimpleIntentDispatcher.cs`
- `src/Sigil.Api/Security/SigilAuthMiddleware.cs`
- `src/Sigil.Api/Endpoints/Agents/RegisterEndpoint.cs` (+ Request DTO)
- `src/Sigil.Api/Endpoints/Agents/DeregisterEndpoint.cs`
- `src/Sigil.Api/Endpoints/Agents/HeartbeatEndpoint.cs`
- `src/Sigil.Api/Endpoints/Agents/ListAgentsEndpoint.cs`
- `src/Sigil.Api/Endpoints/Intents/SubmitIntentEndpoint.cs` (+ Request DTO)
- `tests/Sigil.Api.Tests/Sigil.Api.Tests.csproj`
- `tests/Sigil.Api.Tests/...` (test files listed above)
- `tests/Sigil.Runtime.Tests/Intents/SimpleIntentDispatcherTests.cs`

**Changed:**
- `src/Sigil.Api/Program.cs` — wiring as shown above.
- `src/Sigil.Api/Sigil.Api.csproj` — add `ProjectReference` to `Sigil.Infrastructure`, `Sigil.Runtime`, `Sigil.Storage.EfCore`.
- `src/Sigil.Runtime/DependencyInjection/SigilRuntimeServiceCollectionExtensions.cs` — register `IIntentDispatcher`.
- `src/Sigil.Api/appsettings.Development.json` — sample `OpenTier.Keys` entry.
- `sigil.sln` — add `Sigil.Api.Tests` project.
- `Roadmap.md` — mark `#13` as merged once the PR lands (separate doc commit, per existing pattern).

## Out of scope reminders (won't be touched)

- `Sigil.Core` stays storage-/HTTP-/LLM-free. The new `IIntentDispatcher` interface only references `Sigil.Core.Protocol` types — no infrastructure.
- No audit-log writes in this issue. Phase 2's orchestrator will write `AuditEntry` on commit; the dispatcher stub does not commit context deltas.
- No SignalR hub work; checkpoints are Phase 3.

## Acceptance checklist

- [ ] All five endpoints exist at the documented routes and return the documented status codes.
- [ ] Auth middleware blocks unauthenticated requests with consistent error codes from `SigilSecurityErrors`.
- [ ] `IIntentDispatcher` interface lives in `Sigil.Core`; implementation in `Sigil.Runtime`; endpoint depends only on the interface.
- [ ] `dotnet build sigil.sln` is clean.
- [ ] `dotnet test sigil.sln` passes including the new `Sigil.Api.Tests` and the new dispatcher unit tests.
- [ ] No new dependencies in `Sigil.Core.csproj`.
