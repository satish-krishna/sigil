# Contributing to Sigil

Thanks for contributing to Sigil — a hardened Agent OS. This guide covers the development process for the .NET 9 kernel, the Agent SDK, storage providers, and (once it lands) the Angular dashboard.

## Before You Start

**Read first:**

1. [CLAUDE.md](CLAUDE.md) — orientation and non-negotiable principles
2. [.bob/docs/sigil-architecture-blueprint.md](./.bob/docs/sigil-architecture-blueprint.md) — canonical design (source of architectural truth)
3. [README.md](README.md) — current Phase 1 checklist

If work brushes against one of the [Open questions](./.bob/docs/sigil-architecture-blueprint.md) (snapshot size limits, delta conflict resolution, non-.NET agent support), propose a direction in a plan under `.bob/plans/` — don't commit to one in code.

## Table of Contents

- [Project Layout](#project-layout)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Branching, Commits, and PRs](#branching-commits-and-prs)
- [Code Style and Linting](#code-style-and-linting)
- [Backend Development Guidelines](#backend-development-guidelines)
- [Frontend Development Guidelines](#frontend-development-guidelines)
- [Testing Strategy](#testing-strategy)
- [Security and Secrets](#security-and-secrets)
- [Definition of Done Checklist](#definition-of-done-checklist)

## Project Layout

See [CLAUDE.md → Project layout](CLAUDE.md#project-layout-see-blueprint-72) for the current solution structure. The short version:

```
src/
  Sigil.Core/            # Zero-dependency contracts
  Sigil.Agent.SDK/       # NuGet for agent authors
  Sigil.Storage.Mongo/   # Mongo provider for ISigilStore
  Sigil.Storage.EfCore/  # EF Core provider for ISigilStore
  Sigil.Infrastructure/  # Gateway, JWT/mTLS, observability
  Sigil.Runtime/         # Registry, Orchestrator, Planners, Policies
  Sigil.Api/             # FastEndpoints + SignalR hubs
  agents/                # [planned] Sample agents
  sigil-ui/              # [planned] Angular dashboard
```

`Sigil.Core` must stay free of dependencies on storage, LLM providers, or HTTP. Keep it that way.

## Prerequisites

- **.NET 9 SDK** (`dotnet --version`)
- **Docker Desktop** (for `docker compose up` — kernel + sample agent + MongoDB)
- **Node.js 20+** and npm 10+ (only once `sigil-ui` lands)
- **Git**
- IDE of your choice — Rider, Visual Studio, or VS Code with the C# Dev Kit all work

## Getting Started

```bash
git clone <repo-url>
cd sigil

# Backend
dotnet restore sigil.sln
dotnet build sigil.sln
dotnet test sigil.sln

# Run the API
dotnet run --project src/Sigil.Api

# Full local stack (kernel + sample agent + MongoDB)
docker compose up
docker compose logs -f sigil-api
```

EF migrations (only when the EF provider is the active store):

```bash
dotnet ef migrations add <Name> --project src/Sigil.Storage.EfCore
dotnet ef database update --project src/Sigil.Storage.EfCore
```

## Branching, Commits, and PRs

Sigil uses **[Conventional Commits](https://www.conventionalcommits.org/)** for commit messages, PR titles, and branch names. This is mechanically enforced by CI — PRs with non-conforming titles or commits will fail.

### Branch names

Format: `<type>/<short-kebab-description>`

Examples:

- `feat/snapshot-engine`
- `fix/etag-mismatch-retry`
- `refactor/planner-strategy`
- `docs/blueprint-checkpoints`
- `chore/bump-fastendpoints`

### Commit messages

Format:

```
<type>(<scope>): <description>

<optional body>

<optional footer>
```

**Types:**

- `feat` — new feature
- `fix` — bug fix
- `refactor` — code change that neither fixes a bug nor adds a feature
- `test` — adding or updating tests
- `docs` — documentation only
- `style` — formatting (no behavior change)
- `perf` — performance improvement
- `build` — build system or dependency changes
- `ci` — CI/CD config
- `chore` — other maintenance

**Scopes** (suggested, match the project or subsystem):

`core`, `sdk`, `runtime`, `api`, `storage-mongo`, `storage-ef`, `infra`, `ui`, `docs`, `ci`

**Examples:**

```
feat(runtime): add SnapshotEngine with ETag-based optimistic concurrency

Snapshots are built once per task and pushed to the agent. Agents return
deltas that are committed via IContextStore.CommitDeltaAsync with the
expected ETag. Mismatches trigger a fresh-snapshot retry — no locks.

Closes #42
```

```
fix(api): reject /sigil/execute when /sigil/validate has not succeeded
```

Keep commits focused. A PR may contain multiple commits; each should stand on its own and follow the format. Rebase on `main` before opening a PR — merge commits are rejected.

### Pull requests

- **PR title** follows Conventional Commits (same format as a commit message). The squash-merge commit uses the PR title.
- Link related issues with `Closes #123`.
- Include API request/response examples for wire-protocol changes.
- Include screenshots for UI changes.
- Ensure all CI checks pass: build, test, lint.

## Code Style and Linting

### Backend (.NET 9)

```bash
dotnet build sigil.sln
dotnet test sigil.sln
dotnet format sigil.sln
```

- C# conventions: PascalCase for types/members, camelCase for locals and parameters, `_camelCase` for private fields.
- EditorConfig-driven formatting (`dotnet format` must be clean).
- `Directory.Build.props` controls shared compiler settings — prefer extending it over per-project `<PropertyGroup>`s.
- Nullable reference types are enabled solution-wide. See [Backend Development Guidelines](#backend-development-guidelines) for how to handle "absent value" without `null`.
- xUnit for tests.

### Frontend (Angular, once `sigil-ui` lands)

Guidelines will be added when the UI project is scaffolded. Expect: standalone components, signals, OnPush, `@if`/`@for` control flow, no RxJS in app code, Tailwind + SpartanNG.

## Backend Development Guidelines

Read [CLAUDE.md → Architecture — non-negotiable principles](CLAUDE.md#architecture--non-negotiable-principles) before writing backend code. The rules below are the coding-level expression of those principles.

### Result pattern — never throw for control flow

Use **[CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions)** `Result` / `Result<T>` for any operation that can fail as part of normal business logic: validation failures, capability mismatches, ETag conflicts, missing entities, policy rejections, etc.

**Exceptions are reserved for truly exceptional conditions** — unrecoverable infrastructure failures, programmer errors (null arguments to a contract that forbids them), cancellation. If a caller might reasonably want to handle it, it's a `Result`, not a throw.

```csharp
// ✅ CORRECT
public async Task<Result<Delta>> CommitDeltaAsync(
    TaskId taskId,
    Delta delta,
    ETag expectedETag,
    CancellationToken ct)
{
    var current = await _store.GetAsync(taskId, ct);
    if (current.ETag != expectedETag)
        return Result.Failure<Delta>("etag-mismatch");

    // ... commit ...
    return Result.Success(delta);
}

// ❌ WRONG — throwing for an expected, caller-handleable outcome
if (current.ETag != expectedETag)
    throw new ConcurrencyException("ETag mismatch");
```

Compose with `Bind`, `Map`, `Tap`, `Ensure`. Endpoints map `Result` to HTTP status codes in a single place — don't scatter `try/catch` through handlers.

### Maybe — never return `null` for a value

Use `Maybe<T>` for any reference that can legitimately be absent: "lookup may not find a row," "header may not be present," "optional config." **Do not return `null`** from methods, and do not accept `null` as "no value" in parameters.

```csharp
// ✅ CORRECT
public Task<Maybe<Agent>> FindByIdAsync(AgentId id, CancellationToken ct);

var agent = await registry.FindByIdAsync(id, ct);
return agent.Match(
    onValue: a => Result.Success(a),
    onNone: () => Result.Failure<Agent>("agent-not-found"));

// ❌ WRONG
public Task<Agent?> FindByIdAsync(AgentId id, CancellationToken ct); // null == not found
```

Nullable reference types remain enabled — use `?` only for interop with frameworks that genuinely produce `null` (JSON deserialization of optional fields, some BCL APIs). Convert to `Maybe<T>` at the boundary.

### FastEndpoints

- One endpoint class per route, inheriting from `Endpoint<TRequest, TResponse>`.
- Endpoints stay thin — delegate to services in `Sigil.Runtime` or the relevant module.
- FluentValidation for request validation; invalid requests never reach the handler.
- Map `Result` failures to HTTP status codes in a shared post-processor, not inline.
- Explicit DTOs for every request and response — never expose domain entities directly.

### Storage layer

- Domain code depends on `ISigilStore` / `IContextStore` / `IAuditStore` from `Sigil.Core` — never on Mongo or EF types.
- New capability? Add the contract to `Sigil.Core` and implement it in *both* storage providers, or document why one provider intentionally no-ops.
- EF migrations live in `Sigil.Storage.EfCore`. Never hand-edit generated migration files after they're committed.
- Audit rows are append-only. No `UPDATE`, no `DELETE` — if you find yourself wanting one, you're solving the wrong problem.

### Services and DI

- Register in `Sigil.Api/Program.cs` (or module-specific `AddSigil*` extensions).
- Default lifetime is `Scoped`. Use `Singleton` only for thread-safe, stateless dependencies. Justify `Transient` in a comment.
- Depend on abstractions. If a test needs to mock it, the production code binds to an interface.

### Observability

- All cross-boundary work (orchestrator → agent, store calls, planner invocations) opens a `System.Diagnostics.Activity`. Tag with `task.id`, `agent.id`, `capability`, and (for storage) `operation`.
- Structured logging only. No string interpolation into log messages — use message templates so fields are indexed.

## Frontend Development Guidelines

Placeholder until `src/sigil-ui/` lands. Expected stack: Angular 21 standalone components, signals, SpartanNG, Tailwind, no RxJS in app code.

## Testing Strategy

### Backend

```bash
dotnet test sigil.sln
dotnet test sigil.sln /p:CollectCoverage=true
dotnet test --filter "FullyQualifiedName~SnapshotEngineTests"
```

- xUnit, AAA pattern.
- Prefer focused unit tests on `Sigil.Core` contracts and `Sigil.Runtime` policies.
- Integration tests for the wire protocol (`/sigil/validate`, `/sigil/execute`, delta commit) — run the kernel in-process and hit it with a test agent.
- Storage providers each have their own integration-test project that spins up a real Mongo / SQLite instance. Don't mock the store in provider tests.
- Target >80% coverage for new code. Coverage is a floor, not a ceiling — the goal is meaningful tests, not numbers.

### Wire-protocol changes

If a change touches the agent ↔ kernel protocol, `docker compose up` and exercise the flow end-to-end before declaring done. Unit tests alone can't catch serialization drift.

## Security and Secrets

**Never commit secrets.**

- The PreToolUse hook in `.claude/settings.json` blocks edits to `.env*` and `*.Development.json` — don't bypass it.
- Use environment variables or a local secret store for dev. For production, use a managed secret store (Key Vault / Secret Manager).
- All agent ↔ kernel traffic is authenticated: Sigil-Key (Open), Sigil-Key + JWT (Standard), mTLS + JWT (Trusted). When adding a capability, decide which tier it requires and document it.
- The PII-Cleared flag gates whether an agent sees unscrubbed snapshots. Defaulting to "cleared" is a security bug.
- Validate all external input at the boundary (FastEndpoints request + FluentValidation). Treat agent responses as untrusted.

## Definition of Done Checklist

Before opening a PR, ensure ALL of these pass:

**Build & quality**

- [ ] `dotnet build sigil.sln` succeeds (no errors, no new warnings)
- [ ] `dotnet format sigil.sln --verify-no-changes` is clean
- [ ] No new `null` returns; absent values use `Maybe<T>`
- [ ] No new `throw` for expected failures; use `Result`

**Tests**

- [ ] `dotnet test sigil.sln` passes
- [ ] New or changed code has tests; coverage for touched code stays >80%
- [ ] Wire-protocol changes exercised via `docker compose up`

**Architecture**

- [ ] `Sigil.Core` still has zero dependencies on storage, LLM providers, or HTTP
- [ ] Capability crossing a blueprint boundary (snapshot/delta, ETag, zero-trust, audit, checkpoints) is documented in the PR
- [ ] LLM calls go through `IChatClient` — no provider-specific types leaked

**Security**

- [ ] No secrets, keys, or connection strings committed
- [ ] Auth tier (Open / Standard / Trusted) identified for new endpoints
- [ ] External input validated at the boundary

**Docs & process**

- [ ] Blueprint updated if architectural intent changed
- [ ] Plan in `.bob/plans/` for non-trivial work
- [ ] Branch rebased on latest `main`; no merge commits
- [ ] Commit messages, branch name, and PR title follow Conventional Commits
- [ ] Related issues linked with `Closes #123`

**Final check**

- [ ] "Is this the simplest solution that works?"
- [ ] "Did I fix the actual problem, or just the symptom?"
- [ ] "Would a teammate coming to this cold next quarter understand why?"

## Useful Resources

- [CLAUDE.md](CLAUDE.md) — project instructions for AI and humans alike
- [Sigil Architecture Blueprint](./.bob/docs/sigil-architecture-blueprint.md) — canonical design
- [Conventional Commits](https://www.conventionalcommits.org/)
- [CSharpFunctionalExtensions](https://github.com/vkhorikov/CSharpFunctionalExtensions) — `Result`, `Maybe`, and functional helpers
- [FastEndpoints](https://fast-endpoints.com)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [.NET 9 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)

Thanks for helping build Sigil.
