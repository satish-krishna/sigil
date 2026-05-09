# Roadmap

The path to **Sigil v1**: a runnable kernel, one real agent, persistent storage, and a one-command local stack. Issues are listed in completion order, grouped by layer. Items inside the same layer can be developed in parallel; layers stack on their predecessors.

**v1 demonstrates:**
- A kernel (FastEndpoints) that registers agents, stores their advertised skills, and dispatches intents over a signed gateway
- A real out-of-process agent (Echo) authored as a JSON manifest + Claude-style skills + optional C# hooks
- Persistent storage with optimistic concurrency
- `docker compose up` boots the whole stack

Phases beyond v1 (planner, policy engine, observability, JWT/mTLS) are tracked separately in the blueprint's [Phase Plan](.bob/docs/sigil-architecture-blueprint.md#9-phase-plan).

---

## Status legend

- ✅ Merged
- 🔄 In flight (PR open or active branch)
- ⬜ Not started

---

## Foundation

| Status | Issue | Title |
|---|---|---|
| ✅ | [#1](https://github.com/satish-krishna/sigil/issues/1) | Solution scaffolding |
| ✅ | [#2](https://github.com/satish-krishna/sigil/issues/2) | Core abstractions — `ISigilStore` + `IAuditStore` |
| 🔄 | [PR #19](https://github.com/satish-krishna/sigil/pull/19) | Agent definition refinement (`Skill` replaces `Capability`; first-class `Model` / `Tools` / `Skills` on `AgentRegistration`) |

---

## Phase 1 · Layer 1 — Cross-cutting prep *(parallelizable)*

| Status | Issue | Title | Notes |
|---|---|---|---|
| ⬜ | [#18](https://github.com/satish-krishna/sigil/issues/18) | Central Package Management | Land first to keep package versions consistent across all subsequent projects |
| ⬜ | [#3](https://github.com/satish-krishna/sigil/issues/3) | Agent protocol types (`AgentTask`, `AgentExecutionPackage`, `ValidationRequest`/`Result`) | Required by gateway and SDK |
| ⬜ | [#4](https://github.com/satish-krishna/sigil/issues/4) | Sigil-Key validation (Open tier) | Required by gateway and SDK |

---

## Phase 1 · Layer 2 — Storage *(parallelizable)*

| Status | Issue | Title |
|---|---|---|
| ⬜ | [#5](https://github.com/satish-krishna/sigil/issues/5) | MongoDB provider with ETag concurrency *(used by v1 Docker Compose)* |
| ⬜ | [#6](https://github.com/satish-krishna/sigil/issues/6) | EF Core provider + initial migration |

---

## Phase 1 · Layer 3 — Kernel runtime

| Status | Issue | Title | Depends on |
|---|---|---|---|
| ⬜ | [#7](https://github.com/satish-krishna/sigil/issues/7) | Secure Agent Registry with weighted routing | #2, #4 |
| ⬜ | [#10](https://github.com/satish-krishna/sigil/issues/10) | Secure Gateway (JWT-signed dispatch + Polly) | #3, #4 |
| ⬜ | [#13](https://github.com/satish-krishna/sigil/issues/13) | FastEndpoints — agent lifecycle + intent | #7, #10 |
| ⬜ | [#11](https://github.com/satish-krishna/sigil/issues/11) | Agent Health Monitor | #7, #10 |

---

## Phase 1 · Layer 4 — Agent SDK

| Status | Issue | Title | Depends on |
|---|---|---|---|
| ⬜ | [#20](https://github.com/satish-krishna/sigil/issues/20) | SDK · manifest & skill loader | PR #19 |
| ⬜ | [#8](https://github.com/satish-krishna/sigil/issues/8) | SDK · agent protocol endpoints | #3 |
| ⬜ | [#21](https://github.com/satish-krishna/sigil/issues/21) | SDK · system-prompt composition + lifecycle hooks | #20 |
| ⬜ | [#22](https://github.com/satish-krishna/sigil/issues/22) | SDK · tool invocation (in-process + MCP + HTTP) | #20, #21 |
| ⬜ | [#9](https://github.com/satish-krishna/sigil/issues/9) | SDK · registration, heartbeat, JWT refresh | #4, #20, #13 |

---

## Phase 1 · Layer 5 — Integration

| Status | Issue | Title | Depends on |
|---|---|---|---|
| ⬜ | [#12](https://github.com/satish-krishna/sigil/issues/12) | Echo Agent (sample using SDK) | #8, #9, #20, #21, #22 |
| ⬜ | [#14](https://github.com/satish-krishna/sigil/issues/14) | Docker Compose — Kernel + Echo + MongoDB | #5, #12, #13 |

---

## Dependency graph

```
              Foundation (#1 ✅, #2 ✅, PR #19 🔄)
                              │
              ┌───────────────┼───────────────┐
              │               │               │
            #18              #3              #4
              │               │               │
              │           ┌───┴───┐       ┌───┴───┐
              │           │       │       │       │
              │       #5 / #6   #10     #10     #9
              │           │       │       │       │
              │           │       └───┬───┘       │
              │           │           │           │
              │           │          #13          │
              │           │           │           │
              │           │       #11 (parallel)  │
              │           │                       │
              │           └─────── #20 ───────────┤
              │                     │             │
              │                     ├─── #8 ──────┤
              │                     │             │
              │                     ├─── #21 ─────┤
              │                     │             │
              │                     └─── #22 ─────┤
              │                                   │
              └────────────────────────► #12 ◄────┘
                                          │
                                         #14
                                          │
                                      v1 ready
```

---

## What v1 delivers

After every issue above is closed:

1. **Kernel boots.** `dotnet run --project src/Sigil.Api` exposes register/deregister/heartbeat/list/intent endpoints over FastEndpoints.
2. **Storage persists.** MongoDB holds agent registrations, audit entries, and context with ETag-based optimistic concurrency.
3. **Agents register.** The Echo agent boots from a JSON manifest, advertises one skill (`echo-input`), heartbeats, and gracefully deregisters on shutdown.
4. **Intents flow end-to-end.** A `POST /api/intents` matches an agent by skill, the gateway POSTs `/sigil/execute` over the signed channel, the agent runs the skill (composing the system prompt, invoking the in-process echo tool), returns a `ContextDelta`, and the audit log records the result.
5. **One command runs the stack.** `docker compose up` builds and starts the kernel container, the Echo agent container, and MongoDB.

---

## Beyond v1

Per [blueprint §9](.bob/docs/sigil-architecture-blueprint.md#9-phase-plan):

- **Phase 2** — `IPlanner` (Deterministic / LLM / Hybrid), SnapshotEngine, multi-agent execution with delta merging, second sample agent.
- **Phase 3** — Policy pipeline (token budget, tool access, PII masking, checkpoints), JWT issuance, mTLS for the Trusted tier.
- **Phase 4** — OpenTelemetry tracing, structured log ingestion, cost-per-intent metrics, dashboards.
- **Phase 5** — Angular dashboard, agent marketplace concepts, MCP-as-tool-provider integration.
