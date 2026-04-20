# Sigil

> *A mark of power and binding.* An Agent OS for resilient, secure, and observable AI orchestration.

![Sigil — the Agent OS & Harness](docs/diagrams/sigil.png)

Sigil is a **state-synchronized kernel** that manages, discovers, orchestrates, and observes remote domain-specific AI agents. It is *not* an agent framework — it sits above [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) and provides the OS-level services individual agents shouldn't build themselves: state, security, policy, observability, and audit.

**Agents are ephemeral workers. The Kernel is the sole source of truth.**

---

## Why another layer?

Most "agent frameworks" leave you to rebuild the same load-bearing plumbing every time: how state moves between agents, how tool credentials are scoped, how writes get approved, how you prove what happened. Sigil treats those as kernel concerns so agents only carry domain logic.

| Concern | Sigil's answer |
|---|---|
| State | **Snapshot pushed, delta returned** — zero callbacks from agents to kernel |
| Security | **Zero-trust**: mTLS + JWT + Sigil-Key, three tiers (Open / Standard / Trusted) |
| Concurrency | **Optimistic via ETag** — never a distributed lock |
| Resilience | **Pre-flight `/sigil/validate`** before dispatch, Polly on the gateway |
| Audit | **Immutable `IAuditStore`** on every delta commit |
| Routing | **Weighted registry** for canary / A-B deployments |
| Planning | **`IPlanner` strategy**: Deterministic · LLM · Hybrid — LLM calls via `IChatClient` |
| Writes | **Human checkpoints** — the job pauses until someone resolves |

---

## Architecture at a glance

The kernel sits between an external dispatcher (UI, API, or scheduler) and a fleet of remote agent containers. Every dispatch crosses the **Secure Gateway** as a signed task plus a context snapshot; every response is a delta committed atomically against an ETag and written to the audit log.

- **External dispatch** — Intents from a UI, REST call, or schedule.
- **Orchestrator & Job Manager** — Builds the execution plan (via `IPlanner`), schedules steps, and owns the task graph.
- **Agent Registry** — Capability and version catalog with routing weights.
- **Policy Engine** — Pre-flight guardrails: token budgets, tool access, rate limits, human-checkpoint gates.
- **Context Bus & Audit Log** — Atomic state with ETags; every delta is audited immutably.
- **Secure Agent Gateway** — mTLS + JWT, signed requests, Polly resilience.
- **Agent containers** — Each agent is an out-of-process worker speaking the `/sigil/*` protocol via the Sigil SDK on top of Microsoft Agent Framework.

See [`.bob/docs/sigil-architecture-blueprint.md`](.bob/docs/sigil-architecture-blueprint.md) for the full design, including the Snapshot/Delta pattern, security handshake, planner strategies, and phase plan.

---

## Stack

- **Kernel** — .NET 9, FastEndpoints v8, SignalR
- **Storage** — `ISigilStore` abstraction with two providers: MongoDB and EF Core
- **LLM** — `IChatClient` (Microsoft.Extensions.AI) — swap Claude, OpenAI, Azure, or Ollama with one line
- **Resilience** — Polly (circuit breaker, timeout, retry) in the Secure Gateway
- **Observability** — OpenTelemetry (`System.Diagnostics.Activity`)
- **Agent runtime** — Microsoft Agent Framework (GA 1.0) inside each remote container
- **Frontend** — Angular (standalone components, signals), SpartanNG, Tailwind (later phase)

---

## Project layout

```
sigil/
├── src/
│   ├── Sigil.Core/             # Zero-dependency contracts: protocol, stores, policy, planner
│   ├── Sigil.Agent.SDK/        # NuGet for agent authors — register/heartbeat/validate/snapshot-delta
│   ├── Sigil.Storage.Mongo/    # MongoSigilStore + MongoAuditStore
│   ├── Sigil.Storage.EfCore/   # EfSigilStore + migrations
│   ├── Sigil.Infrastructure/   # Gateway, JWT/mTLS, observability primitives
│   ├── Sigil.Runtime/          # Registry, Orchestrator, SnapshotEngine, Planners, Policies
│   ├── Sigil.Api/              # FastEndpoints + SignalR hubs
│   ├── agents/                 # Sample agents (Echo, Weather, …) using the SDK (later phase)
│   └── sigil-ui/               # Angular dashboard (later phase)
├── sigil.sln
├── global.json
├── Directory.Build.props
├── docs/
│   ├── decks/                  # pptxgenjs + Marp architecture decks
│   └── diagrams/               # System diagrams (SVG + PNG)
├── .bob/docs/                  # Canonical architecture blueprint
└── CLAUDE.md                   # Working agreement for AI-assisted development
```

---

## Status

**Phase 0 → Phase 1 in flight.** The solution is scaffolded and builds clean; domain code has not yet landed.

- [x] Solution scaffolding (7 projects · `global.json` · `Directory.Build.props`)
- [ ] `ISigilStore` + `IAuditStore` abstractions in Core
- [ ] Agent Protocol types (Package, Result, Validation)
- [ ] MongoDB and EF Core storage providers
- [ ] Secure Agent Registry
- [ ] `Sigil.Agent.SDK` (registration, heartbeat, snapshot/delta)
- [ ] Secure Gateway (JWT + Polly)
- [ ] Echo agent + Docker Compose

Later phases cover the planner, policy pipeline, observability, and Angular dashboard. See the blueprint's *Phase Plan* section.

---

## Getting started

> The solution builds, but there's nothing to run yet beyond an empty FastEndpoints host. Commands below will become meaningful as Phase 1 lands.

```bash
# Restore and build
dotnet build sigil.sln

# Run the (currently empty) API
dotnet run --project src/Sigil.Api

# Full local stack — kernel + MongoDB + sample agent (later)
docker compose up
```

---

## Documentation

- **Architecture blueprint** — [`.bob/docs/sigil-architecture-blueprint.md`](.bob/docs/sigil-architecture-blueprint.md)
- **Slide deck (PowerPoint)** — [`docs/decks/sigil-architecture.pptx`](docs/decks/sigil-architecture.pptx)
- **Slide deck (Marp source)** — [`docs/decks/sigil-architecture.md`](docs/decks/sigil-architecture.md)
- **System diagram (SVG)** — [`docs/diagrams/system-overview.svg`](docs/diagrams/system-overview.svg)
- **Working agreement** — [`CLAUDE.md`](CLAUDE.md)

Render the Marp deck locally:

```bash
npx @marp-team/marp-cli docs/decks/sigil-architecture.md -o docs/decks/sigil-architecture.html
```

---

*Sigil — a mark of power and binding.*
