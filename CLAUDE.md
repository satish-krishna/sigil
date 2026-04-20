# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Sigil** — a hardened Agent OS. A .NET kernel that manages, orchestrates, and observes remote domain-specific AI agents running as out-of-process containers. Sigil is *not* an agent framework — it sits above Microsoft Agent Framework and provides the OS-level services (state, security, policy, observability) that individual agents shouldn't build themselves.

> **Status:** Phase 1 in progress. Solution is scaffolded (7 projects, `sigil.sln`, `Directory.Build.props`) and builds clean; domain code has not yet landed. The canonical design is `.bob/docs/sigil-architecture-blueprint.md` — read that before writing code. `README.md` has the current Phase 1 checklist; everything else here is orientation.

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

## Project layout (see blueprint §7.2)

```
src/
  Sigil.Core/                 # [scaffolded] Zero-dependency contracts: protocol, stores, policy, planner
  Sigil.Agent.SDK/            # [scaffolded] NuGet for agent authors — register/heartbeat/validate/snapshot-delta
  Sigil.Storage.Mongo/        # [scaffolded] MongoSigilStore + MongoAuditStore
  Sigil.Storage.EfCore/       # [scaffolded] EfSigilStore + migrations
  Sigil.Infrastructure/       # [scaffolded] Gateway, JWT/mTLS, observability primitives
  Sigil.Runtime/              # [scaffolded] Registry, Orchestrator, SnapshotEngine, Planners, Policies
  Sigil.Api/                  # [scaffolded] FastEndpoints + SignalR hubs
  agents/                     # [planned]    Sample agents (Echo, Weather, …) using the SDK
  sigil-ui/                   # [planned]    Angular dashboard
```

---

## Commands

```bash
# Backend
dotnet build sigil.sln
dotnet test sigil.sln
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

## Documentation

- **Blueprint** (canonical design): `.bob/docs/sigil-architecture-blueprint.md`
- **README**: repo-root overview + Phase 1 checklist
- **Decks**: `docs/decks/sigil-architecture.pptx` (pptxgenjs build script alongside); Marp source at `docs/decks/sigil-architecture.md`
- **Diagrams**: `docs/diagrams/system-overview.svg`, `docs/diagrams/sigil.png`
- **Plans & patterns**: `.bob/plans/` for pre-implementation plans; `.bob/patterns/` for codified patterns once real code lands

---

## Claude Code setup

Key pieces (some infrastructure was seeded from an unrelated Angular/Supabase project and needs review before use on Sigil — flagged below):

- **`.claude/settings.json`** — SessionStart hook loads the *Bob the Skull* persona; PostToolUse runs Prettier on edited `.ts/.html/.scss/.json/.cs`; PreToolUse blocks edits to `.env*` and `*.Development.json`.
- **`.claude/plugins/bob/`** — the `bob` plugin (persona + slash commands).
- **`.claude/commands/bob/`** — `/bob:code-review`, `/bob:fix`, `/bob:raise`, `/bob:approve`, `/bob:reject`, `/bob:simplify`, `/bob:enhance`, `/bob:audit`, `/bob:report`, `/bob:config`.
- **`.claude/agents/`** — `chotu` (engineer), `sirji` (tech lead), `code-reviewer`, `quality-fixer`, `security-reviewer`. **Needs review:** these were authored against an Angular/Supabase/NX/RxJS stack and must be rewritten for the .NET 9 kernel + Microsoft Agent Framework context before direct use.
- **`.claude/skills/`** — generic quality skills (`code-simplifier`, `tech-debt-analyzer`, `test-gap-filler`, `quality-orchestrator`, `enhancement-finder`, `auto-fixer`) are reusable. **Out of scope for this repo:** `authentication`, `capacitor`, `components`, `database`, `fastendpoints`, `forms`, `logging`, `resource-api`, `signals`, `siora-prototype`, `testing`, `frontend-design` — these target an Angular/Supabase stack; evaluate before using and consider removing.
- **`.mcp.json`** — only `playwright` is retained.

---

## Workflow rules

- **Plan before implementing anything non-trivial.** Write the plan to `.bob/plans/` (the directory exists from the seed). The blueprint is the source of architectural truth — the plan references sections, doesn't redefine them.
- **Patterns:** read 2–3 existing files in the same area before adding new code. Once the solution has real code, revisit and codify patterns in `.bob/patterns/`.
- **Precision:** do exactly what's asked; don't expand scope. If a bug fix reveals a design question, flag it — don't silently refactor.
- **Verify before declaring done:** `dotnet build && dotnet test` for backend changes; `docker compose up` for protocol changes that cross the wire.

---

## Open questions (blueprint §10)

Snapshot size limits, delta conflict resolution strategy, and non-.NET agent support are intentionally unresolved. When work brushes against them, propose a direction in a plan — don't commit to one in code.
