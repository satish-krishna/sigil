---
marp: true
theme: default
size: 16:9
paginate: true
title: Sigil — Architecture Blueprint
author: Sigil
style: |
  :root {
    --navy: #1E2761;
    --deep: #0F1530;
    --mid:  #3B4BA3;
    --amber: #F9B87A;
    --coral: #F96167;
    --accent: #CADCFC;
    --paper: #F5F7FF;
    --card: #FFFFFF;
    --body: #2A2F4A;
    --muted: #6B7394;
    --rule: #D8DEF2;
  }
  section {
    font-family: Calibri, "Segoe UI", system-ui, sans-serif;
    background: var(--paper);
    color: var(--body);
    padding: 56px 72px;
    border-left: 8px solid var(--navy);
    font-size: 22px;
  }
  section::before {
    content: "";
    position: absolute;
    left: 72px;
    top: 40px;
    width: 20px;
    height: 6px;
    background: var(--amber);
  }
  section h1 {
    font-family: Georgia, serif;
    color: var(--navy);
    font-size: 48px;
    margin: 0 0 4px 0;
  }
  section h2 {
    font-family: Georgia, serif;
    color: var(--mid);
    font-style: italic;
    font-weight: normal;
    font-size: 22px;
    margin: 0 0 24px 0;
  }
  section h3 {
    font-family: Georgia, serif;
    color: var(--navy);
    font-size: 22px;
    margin: 0 0 8px 0;
  }
  section p, section li { color: var(--body); line-height: 1.45; }
  section code { font-family: Consolas, monospace; background: #EEF1FB; padding: 1px 6px; border-radius: 4px; color: var(--deep); }
  section pre { background: var(--deep); color: var(--accent); padding: 14px 18px; border-radius: 8px; font-size: 16px; }
  section pre code { background: transparent; color: var(--accent); padding: 0; }
  section footer { color: var(--muted); font-size: 11px; letter-spacing: 2px; }
  section::after { color: var(--muted); font-size: 11px; }
  a { color: var(--mid); }

  /* ── Title slides ── */
  section.title {
    background: var(--deep);
    color: var(--paper);
    border-left: 14px solid var(--amber);
    padding: 96px 96px;
  }
  section.title::before { display: none; }
  section.title h1 {
    color: var(--paper);
    font-size: 104px;
    letter-spacing: 8px;
    margin-bottom: 18px;
  }
  section.title h2 {
    color: var(--accent);
    font-size: 32px;
    font-style: italic;
  }
  section.title .rule {
    width: 100px; height: 4px; background: var(--amber); margin: 28px 0;
  }
  section.title p { color: var(--paper); font-size: 20px; }
  section.title::after { color: var(--accent); }

  /* ── Layout helpers ── */
  .cols  { display: grid; gap: 20px; }
  .c2    { grid-template-columns: 1fr 1fr; }
  .c3    { grid-template-columns: 1fr 1fr 1fr; }
  .c4    { grid-template-columns: repeat(4, 1fr); }

  .card {
    background: var(--card);
    border: 1px solid var(--rule);
    border-radius: 10px;
    padding: 14px 18px;
    border-left: 5px solid var(--navy);
  }
  .card.amber  { border-left-color: var(--amber); }
  .card.coral  { border-left-color: var(--coral); }
  .card.dark   { background: var(--navy); color: var(--paper); border: none; border-left: 5px solid var(--amber); }
  .card.dark h3 { color: var(--amber); }
  .card.dark p, .card.dark li { color: var(--paper); }

  .pill {
    display: inline-block; padding: 2px 10px; border-radius: 10px;
    background: var(--navy); color: var(--paper); font-size: 12px;
    letter-spacing: 2px; font-weight: bold;
  }
  .pill.amber  { background: var(--amber); color: var(--deep); }
  .pill.coral  { background: var(--coral); }
  .pill.mid    { background: var(--mid); }

  table {
    width: 100%; border-collapse: collapse; font-size: 18px;
  }
  th {
    text-align: left; color: var(--muted); font-weight: normal;
    letter-spacing: 2px; font-size: 12px; padding: 8px 12px;
    border-bottom: 2px solid var(--rule);
  }
  td { padding: 10px 12px; border-bottom: 1px solid var(--rule); vertical-align: top; }
  td.k { font-weight: bold; color: var(--navy); white-space: nowrap; }
  td code { font-size: 16px; }

  .mono { font-family: Consolas, monospace; }
  .muted { color: var(--muted); }
  .amber { color: var(--amber); }
  .coral { color: var(--coral); }
---

<!-- _class: title -->

# SIGIL

## A Hardened Agent OS

<div class="rule"></div>

State-synchronized kernel. Zero-trust agents. Snapshot &amp; delta.
Pluggable planner. Immutable audit. Checkpointed writes.

Architecture Blueprint  ·  .NET 9 kernel  ·  Microsoft Agent Framework agents

---

# Vision
## A state-synchronized kernel — not another agent framework

<div class="cols c2">
<div>

Sigil manages, discovers, orchestrates, and observes remote domain-specific AI agents. It sits **above** Microsoft Agent Framework and provides the OS-level services individual agents shouldn't build themselves.

> *Agents are ephemeral workers.*
> *The Kernel is the sole source of truth.*

</div>
<div class="card dark">

### Design Pillars

- **State** — Snapshot → Delta (no callbacks)
- **Security** — mTLS + JWT + Sigil-Key
- **Resilience** — Pre-flight validation
- **Concurrency** — Optimistic via ETags
- **Audit** — Immutable trail
- **Routing** — Weighted / canary
- **Planning** — Deterministic · LLM · Hybrid

</div>
</div>

---

# Core Analogy
## Traditional OS concepts mapped to Sigil

<div class="cols c2">
<div>

| Traditional OS | Sigil |
|---|---|
| Processes | Domain Agents |
| Devices & Drivers | Tools / APIs / MCP |
| Context Switch | **Context Snapshot** |
| System Calls | **Secure Gateway** |
| Virtual Memory | **Atomic Context Bus** |

</div>
<div>

| Traditional OS | Sigil |
|---|---|
| BIOS POST | **Pre-flight Validation** |
| Process Scheduler | Orchestrator |
| Service Registry | Agent Registry |
| Kernel Policies | Policy Engine |
| top / htop | Observability Dashboard |

</div>
</div>

---

# Architecture Overview
## Frontend · Kernel · Remote agent containers

<div class="cols c3">
<div class="card dark" style="background: var(--mid)">

### Angular Frontend
- Dashboard
- Agent Monitor
- Job Viewer
- Checkpoint Queue
- Intent Console
- Audit Explorer

</div>
<div class="card dark">

### Sigil Kernel (.NET 9)
- Registry (versions · weights)
- Orchestrator + Snapshot Engine
- Policy Engine
- `ISigilStore` · `IAuditStore`
- Security Layer (JWT / mTLS)
- Secure Gateway (Polly)

</div>
<div class="card dark" style="background: var(--deep)">

### Secure Agent Containers
- Sigil Agent SDK
- MS Agent Framework
- Tools / APIs / MCP
- mTLS + JWT
- `/sigil/validate`
- `/sigil/execute`

</div>
</div>

<p class="muted" style="margin-top: 24px; font-size: 14px;">Frontend ↔ Kernel via SignalR + REST · Kernel ↔ Agents via signed HTTP (snapshot out / delta back)</p>

---

# Agent Protocol
## Every remote agent exposes these HTTP endpoints

<table>
<thead><tr><th>METHOD</th><th>ENDPOINT</th><th>PURPOSE</th></tr></thead>
<tbody>
<tr><td class="k"><span class="pill amber">POST</span></td><td><code>/sigil/validate</code></td><td>Pre-flight — can the agent handle this task right now?</td></tr>
<tr><td class="k"><span class="pill amber">POST</span></td><td><code>/sigil/execute</code></td><td>Receive Task + Snapshot, return Delta</td></tr>
<tr><td class="k"><span class="pill mid">GET</span></td><td><code>/sigil/health</code></td><td>Liveness + capability status</td></tr>
<tr><td class="k"><span class="pill amber">POST</span></td><td><code>/sigil/cancel/{taskId}</code></td><td>Cancel a running task</td></tr>
<tr><td class="k"><span class="pill mid">GET</span></td><td><code>/sigil/info</code></td><td>Agent metadata (id, domain, capabilities, version)</td></tr>
</tbody>
</table>

---

# Pre-flight Validation
## Prevents zombie tasks — agents accepting work they can't finish

<div class="cols c2">
<div>

1. **Orchestrator** — evaluates policy (budget, access)
2. **Gateway** — `POST /sigil/validate` with task preview
3. **Agent** — returns `ValidationResult`
4. **Passed** → dispatch with Snapshot
   **Failed** → try next agent or fail job

</div>
<div class="card dark">

### ValidationResult

```csharp
public record ValidationResult
{
    public bool      CanHandle       { get; init; }
    public int       EstimatedTokens { get; init; }
    public string[]  MissingTools    { get; init; }
    public string?   Reason          { get; init; }
}
```

Orchestrator uses estimates to enforce token budget *before* dispatch.

</div>
</div>

---

# Snapshot & Delta
## Solves the chatty-context problem — zero callbacks

<div class="cols c4">
<div class="card dark"><h3>Kernel</h3>GetSnapshot(jobId)</div>
<div class="card dark" style="background: var(--mid)"><h3>Policy</h3>scrub PII · inject creds</div>
<div class="card dark" style="background: var(--deep)"><h3>Agent</h3>/sigil/execute · processes locally</div>
<div class="card dark"><h3>Context Bus</h3>CommitDelta(ETag)</div>
</div>

<div class="cols c2" style="margin-top: 20px">
<div class="card amber">

### AgentExecutionPackage (to agent)
- `Task`
- `ContextSnapshot` (key/value)
- `ETag` (concurrency token)
- `ScopedCredentials` (per-tool)

</div>
<div class="card coral">

### AgentExecutionResult (from agent)
- `TaskId`, `Success`
- `StateUpdates` (delta)
- `Logs` (structured)
- `UsageMetrics` (tokens, duration)

</div>
</div>

---

# Secure Agent Registry
## Registration · weighted routing · lifecycle

<div class="cols c2">
<div>

### Lifecycle

`Starting` → `Healthy` → `Degraded` → `Offline` → `Draining`

<p class="muted" style="font-size: 14px;">Heartbeat every 15s · 3 missed → Offline · Stale cleanup on extended offline.</p>

### Weighted Routing

Stable agent · weight **90** (90% traffic)
Canary agent · weight **10** (10% traffic)

*A/B and canary deployments with no code changes.*

</div>
<div>

### Security Tiers

| Tier | Auth | PII |
|---|---|---|
| **Open** | Sigil-Key | No |
| **Standard** | Sigil-Key + JWT | No |
| **Trusted** | mTLS + JWT | Yes (PII-Cleared) |

Tier controls whether the agent ever sees unscrubbed snapshots.

</div>
</div>

---

# Planner
## `IPlanner` strategy — decomposes intent into an execution plan

<div class="cols c3">
<div class="card dark">

### Deterministic
*Capability match + weighted selection*

Zero LLM dependency.
Ships with Sigil Core.

</div>
<div class="card dark" style="background: var(--amber); color: var(--deep)">

### Hybrid
*Deterministic first, LLM fallback*

Recommended default.
Escalates when ambiguous.

</div>
<div class="card dark" style="background: var(--deep)">

### LLM-Only
*`IChatClient` decomposition*

System prompt built
from the live registry.

</div>
</div>

<div class="card" style="margin-top: 20px">

### `IChatClient` — provider-agnostic (Microsoft.Extensions.AI)

<div class="cols c4" style="margin-top: 8px">
<div><span class="pill amber">1</span> Anthropic (Claude)</div>
<div><span class="pill amber">2</span> Azure OpenAI</div>
<div><span class="pill amber">3</span> OpenAI</div>
<div><span class="pill amber">4</span> Ollama (local)</div>
</div>

</div>

---

# Orchestrator
## Plan · validate · snapshot · dispatch · commit · audit

<div class="cols c4">
<div class="card"><h3 class="amber">01</h3>**Submit Intent**<br/><span class="muted">User or API</span></div>
<div class="card"><h3 class="amber">02</h3>**Plan**<br/><span class="muted">IPlanner → ExecutionPlan</span></div>
<div class="card"><h3 class="amber">03</h3>**Pre-flight**<br/><span class="muted">Policy + scoped creds</span></div>
<div class="card"><h3 class="amber">04</h3>**Validate**<br/><span class="muted">POST /sigil/validate</span></div>
</div>

<div class="cols c4" style="margin-top: 20px">
<div class="card"><h3 class="amber">05</h3>**Snapshot**<br/><span class="muted">GetSnapshot + ETag</span></div>
<div class="card"><h3 class="amber">06</h3>**Dispatch**<br/><span class="muted">POST /sigil/execute</span></div>
<div class="card"><h3 class="amber">07</h3>**Commit Delta**<br/><span class="muted">CommitDelta(ETag)</span></div>
<div class="card"><h3 class="amber">08</h3>**Audit**<br/><span class="muted">LogChangeAsync</span></div>
</div>

---

# Atomic Context Bus
## Optimistic concurrency via ETags — no distributed locks

<div class="cols c2">
<div>

### `IContextStore`

```csharp
Task<(Snapshot, ETag)>
    GetSnapshotAsync(jobId);

Task<bool> CommitDeltaAsync(
    jobId, delta, expectedETag);

Task AppendLogAsync(jobId, entry);
Task<IReadOnlyList<AgentLogEntry>>
    GetLogAsync(jobId);
```

</div>
<div class="card dark">

### Concurrent writers

- State: `{ count: 1 }` · ETag `abc`
- Agent A reads — ETag `abc`
- Agent B reads — ETag `abc`
- <span class="amber">A commits ✓</span> — new ETag `def`
- <span class="coral">B commits ✗</span> — conflict
- Orchestrator retries B with fresh snapshot

</div>
</div>

---

# Policy Engine
## Enforced pre-flight — before the agent ever receives work

<table>
<thead><tr><th>POLICY</th><th>STAGE</th><th>DESCRIPTION</th></tr></thead>
<tbody>
<tr><td class="k">Token Budget</td><td><span class="pill">Pre-flight</span></td><td>Estimated cost vs remaining budget</td></tr>
<tr><td class="k">Tool Access</td><td><span class="pill">Pre-flight</span></td><td>Scoped credentials per step only</td></tr>
<tr><td class="k">PII Masking</td><td><span class="pill">Pre-flight</span></td><td>Scrub snapshot if not <code>IsPiiCleared</code></td></tr>
<tr><td class="k">Checkpoint</td><td><span class="pill">Pre-flight</span></td><td>Human approval for writes</td></tr>
<tr><td class="k">Rate Limiting</td><td><span class="pill">Pre-flight</span></td><td>Concurrent jobs · requests/min</td></tr>
<tr><td class="k">Timeout</td><td><span class="pill mid">Dispatch</span></td><td>Polly — per step, per job</td></tr>
<tr><td class="k">Retry</td><td><span class="pill mid">Dispatch</span></td><td>Polly circuit breaker</td></tr>
</tbody>
</table>

---

# Checkpoints
## Non-negotiable for writes — human-in-the-loop for all mutations

<div class="cols c2">
<div>

1. Agent returns delta requesting a write
2. Policy Engine evaluates checkpoint policy
3. Job status → **Paused**
4. SignalR pushes approval card to UI
5. User approves or rejects
6. Resume and commit — or abort

</div>
<div class="card" style="background: var(--coral); border: none;">

<h1 style="color: white; font-size: 60px; margin: 0;">Writes<br/>Pause.</h1>

<div style="width: 60px; height: 3px; background: white; margin: 20px 0;"></div>

<p style="color: white; font-style: italic; font-size: 18px;">
Any write-side capability runs through the checkpoint policy. The job waits on a SignalR-delivered approval before committing.
</p>

</div>
</div>

---

# Security Model
## Zero-trust — all traffic authenticated and signed

<div class="cols c3">
<div class="card dark"><h3>Register</h3>Sigil-Key or mTLS cert → short-lived Agent-JWT</div>
<div class="card dark"><h3>Execute</h3>Kernel signs dispatch · Agent verifies · Delta signed with JWT</div>
<div class="card dark"><h3>Refresh</h3>SDK rotates JWT before expiry</div>
</div>

<table style="margin-top: 24px">
<thead><tr><th>TIER</th><th>AUTH</th><th>PII</th><th>USE CASE</th></tr></thead>
<tbody>
<tr><td class="k">Open</td><td><code>Sigil-Key</code></td><td>No</td><td>Dev / local agents</td></tr>
<tr><td class="k">Standard</td><td><code>Sigil-Key + JWT</code></td><td>No</td><td>Prod agents without sensitive data</td></tr>
<tr><td class="k">Trusted</td><td><code>mTLS + JWT</code></td><td>Yes (PII-Cleared)</td><td>Agents handling personal data</td></tr>
</tbody>
</table>

---

# Storage Abstraction
## `ISigilStore` · two providers · zero kernel dependency on a database

<div class="cols c3">
<div class="card">

### Sigil.Storage.Mongo
- `MongoSigilStore`
- `MongoAuditStore`

```csharp
.UseMongo(opts => {
  opts.ConnectionString = "...";
  opts.Database = "sigil";
});
```

</div>
<div class="card dark">

### `ISigilStore`
- Agents
- Jobs
- Contexts
- Checkpoints
- **Audit (immutable)**

</div>
<div class="card">

### Sigil.Storage.EfCore
- `EfSigilStore`
- `EfAuditStore`
- `SigilDbContext` + Migrations

```csharp
.UseEfCore(opts => {
  opts.UseNpgsql(cs);
});
```

</div>
</div>

<p class="muted" style="margin-top: 18px; font-size: 14px;">Every context change writes an <code>AuditEntry</code>: JobId · AgentId · StepId · Delta · Metrics · Timestamp. Never mutated. Never deleted.</p>

---

# Observability
## OpenTelemetry · structured logs in deltas · cost tracking

<div class="cols c3">
<div class="card dark"><h3>Traces</h3>`Job → Planner → Step → Tool` span hierarchy via `System.Diagnostics.Activity`</div>
<div class="card dark" style="background: var(--mid)"><h3>Logs</h3>Structured JSON returned in the Delta package by each agent</div>
<div class="card dark" style="background: var(--deep)"><h3>Metrics</h3>Cost-per-intent, token usage, latency, success rate → Prometheus / Grafana</div>
</div>

<div class="card" style="margin-top: 20px">

### Trace hierarchy

```
🔵 Job (root)
  ├── Planner.Plan              (det + LLM, tokens: 450)
  ├── Policy.PreFlight
  ├── Agent.Validate
  ├── Context.GetSnapshot
  ├── Agent.Execute
  │     ├── Tool.CreateInvoice
  │     └── Tool.SendEmail
  ├── Context.CommitDelta
  └── Audit.LogChange
```

</div>

---

# Agent SDK
## Agent authors write domain logic — the SDK handles everything else

<div class="cols c2">
<div class="card dark">

### Handled by the SDK
- Self-registration (Sigil-Key or mTLS)
- Heartbeat + deregistration
- Short-lived JWT — auto refresh
- `/sigil/validate` endpoint
- `/sigil/execute` endpoint
- Snapshot / Delta plumbing
- Request signing + verification

</div>
<div>

```csharp
public class ResearchHandler : ISigilAgentHandler
{
  public async Task<AgentExecutionResult>
    ExecuteAsync(
      AgentExecutionPackage package,
      CancellationToken ct)
  {
    var topic = package.ContextSnapshot
      .Get<string>("topic");
    var summary = await _researcher
      .ResearchAsync(topic, ct);
    return new AgentExecutionResult {
      Success = true,
      StateUpdates = { ["summary"] = summary }
    };
  }
}
```

</div>
</div>

---

# Angular Frontend
## The operational dashboard — desktop environment of the Agent OS

<div class="cols c2">
<div>

- **Dashboard** — Active jobs · health grid · cost
- **Agent Catalog** — Capabilities · tier · weight
- **Job Monitor** — Trace waterfall · delta inspector
- **Job History** — Search and replay with audit

</div>
<div>

- **Checkpoint Queue** — Pending human approvals
- **Intent Console** — Submit and watch live
- **Audit Explorer** — Immutable change history

</div>
</div>

<p class="muted" style="margin-top: 40px; text-align: center; font-style: italic;">Angular · standalone components · signals · SpartanNG · Tailwind · SignalR live updates</p>

---

# Project Structure
## Seven libraries under `src/` · agents and UI alongside

<table>
<thead><tr><th>PROJECT</th><th>PURPOSE</th></tr></thead>
<tbody>
<tr><td class="k"><code>Sigil.Core</code></td><td>Zero-dependency contracts: protocol, stores, policy, planner</td></tr>
<tr><td class="k"><code>Sigil.Agent.SDK</code></td><td>NuGet for agent authors — register, heartbeat, validate, snapshot/delta</td></tr>
<tr><td class="k"><code>Sigil.Storage.Mongo</code></td><td>MongoSigilStore + MongoAuditStore</td></tr>
<tr><td class="k"><code>Sigil.Storage.EfCore</code></td><td>EfSigilStore + migrations</td></tr>
<tr><td class="k"><code>Sigil.Infrastructure</code></td><td>Gateway, JWT/mTLS, observability primitives</td></tr>
<tr><td class="k"><code>Sigil.Runtime</code></td><td>Registry, Orchestrator, SnapshotEngine, Planners, Policies</td></tr>
<tr><td class="k"><code>Sigil.Api</code></td><td>FastEndpoints + SignalR hubs</td></tr>
<tr><td class="k"><code>agents/</code></td><td>Sample agents (Echo, Weather, …) using the SDK</td></tr>
<tr><td class="k"><code>sigil-ui/</code></td><td>Angular dashboard</td></tr>
</tbody>
</table>

---

# Key Design Decisions
## Where the architecture made a specific, load-bearing choice

<div class="cols c2">
<div>

| Decision | Choice |
|---|---|
| Agent hosting | Out-of-process containers |
| State model | Snapshot & Delta |
| Concurrency | Optimistic via ETag |
| Security | mTLS + JWT + Sigil-Key |
| Pre-flight | `/sigil/validate` before dispatch |
| Routing | Weighted for canary / A-B |

</div>
<div>

| Decision | Choice |
|---|---|
| Audit | Immutable `IAuditStore` |
| Storage | Mongo + EF Core |
| Planner | `IPlanner` strategy in Core |
| LLM | `IChatClient` (MS.Extensions.AI) |
| Checkpoints | Non-negotiable for writes |
| Observability | OTel + structured delta logs |

</div>
</div>

---

# Phase Plan
## Foundation → Orchestration → Policy → Observability → UI → Polish

<table>
<thead><tr><th>PHASE</th><th>TITLE</th><th>DETAIL</th></tr></thead>
<tbody>
<tr><td class="k"><span class="pill amber">Phase 1</span></td><td>Foundation + Security</td><td>Solution scaffold · stores · protocol · registry · Echo agent · Docker Compose</td></tr>
<tr><td class="k"><span class="pill">Phase 2</span></td><td>Orchestration & Planner</td><td>IPlanner (Det / LLM / Hybrid) · Snapshot engine · ETag commit · audit</td></tr>
<tr><td class="k"><span class="pill">Phase 3</span></td><td>Policy & Zero-Trust</td><td>Policy pipeline · token/tool/PII/checkpoint · JWT + mTLS · Polly</td></tr>
<tr><td class="k"><span class="pill">Phase 4</span></td><td>Observability</td><td>OTel · cost metrics · job traces · structured logs</td></tr>
<tr><td class="k"><span class="pill">Phase 5</span></td><td>Angular Frontend</td><td>Dashboard · catalog · monitor · audit · checkpoints · intent console</td></tr>
<tr><td class="k"><span class="pill">Phase 6</span></td><td>Polish & Extend</td><td>Canary routing · parallel execution · MCP tools · Prometheus export</td></tr>
</tbody>
</table>

---

<!-- _class: title -->

# *A mark of power*
# *and binding.*

<div class="rule"></div>

Sigil — a hardened Agent OS.
Kernel as the source of truth. Agents as ephemeral workers.

Blueprint · `.bob/docs/sigil-architecture-blueprint.md`
