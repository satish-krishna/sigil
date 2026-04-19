# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Sigil** — a hardened Agent OS. A .NET kernel that manages, orchestrates, and observes remote domain-specific AI agents running as out-of-process containers. Sigil is *not* an agent framework — it sits above Microsoft Agent Framework and provides the OS-level services (state, security, policy, observability) that individual agents shouldn't build themselves.

> **Status:** Phase 0 — implementation has not started. The canonical design is `.bob/docs/sigil-architecture-blueprint.md`. Read that before writing code; everything else in this file is orientation.

---

## Stack

- **Kernel:** .NET 9, FastEndpoints v8, SignalR
- **Storage:** `ISigilStore` abstraction — two providers (`Sigil.Storage.Mongo`, `Sigil.Storage.EfCore`)
- **Frontend:** Angular (standalone components, signals) + SpartanNG + Tailwind CSS
- **LLM abstraction:** `IChatClient` (Microsoft.Extensions.AI) — provider-agnostic
- **Observability:** OpenTelemetry (`System.Diagnostics.Activity`)
- **Resilience:** Polly (circuit breaker, timeout, retry) in the Secure Gateway
- **Agent runtime:** Microsoft Agent Framework (GA 1.0) inside each remote agent container

---

## Architecture — non-negotiable principles

Read the blueprint before touching any of these. They are load-bearing.

| Principle | What it means in code |
|---|---|
| **Out-of-process agents** | The kernel must never host agent logic. Agents are remote HTTP endpoints that self-register via the SDK. |
| **Snapshot & Delta** | The kernel bundles all context into a snapshot and pushes it with the task. Agents return only deltas. **No callbacks to the kernel from inside an agent.** |
| **Optimistic concurrency via ETag** | `IContextStore.CommitDeltaAsync` takes an `expectedETag`. On mismatch, the orchestrator retries with a fresh snapshot — never a distributed lock. |
| **Pre-flight validation** | `/sigil/validate` is called *before* `/sigil/execute`. Token budget, tool access, and capability fit are checked up front to prevent zombie tasks. |
| **Zero-Trust** | All agent ↔ kernel traffic is authenticated: Sigil-Key (Open tier), Sigil-Key + JWT (Standard), mTLS + JWT (Trusted). The PII-Cleared flag controls whether the agent ever sees unscrubbed snapshots. |
| **Immutable audit** | Every delta commit writes an `AuditEntry` via `IAuditStore`. Never mutate or delete audit rows. |
| **`IPlanner` strategy** | Plan building is swappable: `Deterministic`, `LlmOnly`, `Hybrid`. The orchestrator must not know which is active. LLM calls go through `IChatClient` — never a specific provider. |
| **Checkpoints for writes** | Any write-side capability runs through the checkpoint policy. The job pauses and emits a SignalR event until a human resolves it. |

**Before implementing a new capability**, check if it crosses one of these boundaries and which interfaces in `Sigil.Core` it must honor. The `Sigil.Core` project has **zero dependencies on storage, LLM providers, or HTTP** — keep it that way.

---

## Project layout (planned — see blueprint §7.2)

```
src/
  Sigil.Core/                 # Zero-dependency contracts: protocol, stores, policy, planner
  Sigil.Agent.SDK/            # NuGet for agent authors — register/heartbeat/validate/snapshot-delta
  Sigil.Storage.Mongo/        # MongoSigilStore + MongoAuditStore
  Sigil.Storage.EfCore/       # EfSigilStore + migrations
  Sigil.Infrastructure/       # Gateway, JWT/mTLS, observability primitives
  Sigil.Runtime/              # Registry, Orchestrator, SnapshotEngine, Planners, Policies
  Sigil.Api/                  # FastEndpoints + SignalR hubs
  agents/                     # Sample agents (Echo, Weather, …) using the SDK
  sigil-ui/                   # Angular dashboard
```

---

## Commands

The solution does not exist yet. As projects land, these are the expected workflows.

```bash
# Backend
dotnet build
dotnet test
dotnet run --project src/Sigil.Api

# Frontend (once sigil-ui lands)
cd src/sigil-ui && npm install
npm run start           # dev server
npm run build
npm test

# Full local stack (kernel + sample agent + MongoDB)
docker compose up
docker compose logs -f sigil-api

# EF Core (only when EF provider is the active store)
dotnet ef migrations add <Name> --project src/Sigil.Storage.EfCore
dotnet ef database update --project src/Sigil.Storage.EfCore
```

---

## Claude Code setup

Infrastructure was seeded from the Siora project. Key pieces:

- **`.claude/settings.json`** — SessionStart hook loads the *Bob the Skull* persona; PostToolUse runs Prettier on edited `.ts/.html/.scss/.json/.cs`; PreToolUse blocks edits to `.env*` and `*.Development.json`.
- **`.claude/plugins/bob/`** — the `bob` plugin (persona + slash commands).
- **`.claude/commands/bob/`** — `/bob:code-review`, `/bob:fix`, `/bob:raise`, `/bob:approve`, `/bob:reject`, `/bob:simplify`, `/bob:enhance`, `/bob:audit`, `/bob:report`, `/bob:config`.
- **`.claude/agents/`** — `chotu` (engineer), `sirji` (tech lead), `code-reviewer`, `quality-fixer`, `security-reviewer`. **These were authored for the Siora stack (Supabase/NX/RxJS rules) and need rewriting for Sigil before direct use.**
- **`.claude/skills/`** — generic quality skills (`code-simplifier`, `tech-debt-analyzer`, `test-gap-filler`, `quality-orchestrator`, `enhancement-finder`, `auto-fixer`) are reusable. Skills named `authentication`, `capacitor`, `components`, `database`, `fastendpoints`, `forms`, `logging`, `resource-api`, `signals`, `siora-prototype`, `testing`, `frontend-design` are Siora-specific — evaluate before using.
- **`.mcp.json`** — only `playwright` is retained. Siora's `nx-mcp` and `supabase` MCP servers do not apply here.

---

## Workflow rules

- **Plan before implementing anything non-trivial.** Write the plan to `.bob/plans/` (the directory exists from the seed). The blueprint is the source of architectural truth — the plan references sections, doesn't redefine them.
- **Patterns:** read 2–3 existing files in the same area before adding new code. Once the solution has real code, revisit and codify patterns in `.bob/patterns/`.
- **Precision:** do exactly what's asked; don't expand scope. If a bug fix reveals a design question, flag it — don't silently refactor.
- **Verify before declaring done:** `dotnet build && dotnet test` for backend changes; `docker compose up` for protocol changes that cross the wire.

---

## Open questions (blueprint §10)

Snapshot size limits, delta conflict resolution strategy, and non-.NET agent support are intentionally unresolved. When work brushes against them, propose a direction in a plan — don't commit to one in code.
