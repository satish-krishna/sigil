---
name: tech-debt-analyzer
description: >
  Use when the user wants a BROAD audit of a codebase, directory, or feature area for tech debt —
  memory leaks, SOLID/DRY/YAGNI violations, dead code, security issues, committed secrets,
  build-config drift (framework versions, wildcard NuGet versions, central package management
  inconsistencies), or violations of documented project standards (CLAUDE.md / CONTRIBUTING.md
  rules, e.g. a no-RxJS rule). Trigger on phrases like "audit this codebase", "what tech debt
  should I fix first", "find dead code", "review libs/X for cleanup", "take stock of tech debt",
  "what's rotting", "getting gnarly", "pre-refactor reconnaissance", or "pre-cleanup sprint".
  Also trigger when the user lists specific debt categories (memory leaks, subscription cleanup,
  SOLID violations, DRY, YAGNI, RxJS in the wrong layer, architectural issues, dead services).
  Fire even without the words "tech debt" — "what's wrong with this module", "needs cleanup", or
  "lots of dead code lurking" all count. Also trigger when `quality-orchestrator` dispatches an
  analysis scan.

  Do NOT trigger when: (1) the user wants to simplify ONE specific function or reduce its
  complexity — use `code-simplifier` instead; (2) the user wants to find missing features, UX
  patterns, accessibility gaps, or API ergonomics improvements — use `enhancement-finder`
  instead; (3) the user is asking you to fix a specific bug they already identified, profile a
  specific endpoint, add/write a new endpoint or component, explain how existing code works, or
  review a single PR/diff. This skill produces a multi-file audit report with a structured
  findings backlog — it is the wrong tool for single-file, single-fix, or "help me understand
  this" tasks.
---

# Tech Debt Analyzer

You are performing a structured code quality audit. Your job is to surface real, actionable issues — not theoretical concerns. Prioritize findings that would cause bugs, degrade performance, or meaningfully slow down future development.

## Step 1: Discover Repo Standards

Before anything else, look for standards defined in the repo itself. These take priority over generic best practices.

Read these files if they exist (don't fail if missing):

- `CLAUDE.md` — project conventions, stack-specific rules, gotchas
- `CONTRIBUTING.md` — contribution guidelines, code style requirements
- `.editorconfig` — formatting rules
- `eslint.config.*` or `.eslintrc.*` — linting rules
- Any `.bob/guides/standards.md` or similar guide files

Extract the rules that are actually actionable — things like "no RxJS", "use signals only", "use FastEndpoints", naming conventions, forbidden patterns. These become your highest-priority check layer.

## Step 2: Detect the Tech Stack

Scan the repo to identify relevant technologies:

- `package.json` — look for Angular, React, Vue, RxJS, etc.
- `*.csproj` or `Directory.Build.props` — look for .NET version, key packages
- `angular.json` or `nx.json` — Angular/NX workspace

Based on what you find, decide which reference files to load:

- **.NET detected** → read `references/dotnet.md`
- **Angular detected** → read `references/angular.md`
- Both detected → read both

## Step 3: Determine Scope

If the user specified a file, directory, or feature area — focus there. If no scope was given, do a **broad pass across all architectural layers**. You must read a minimum of 18-20 files before forming conclusions — reading fewer almost always means missing real issues.

**For each area you audit, cover all of these layers:**

### Backend layers to cover:

- **Entry points:** all endpoint/controller files (not just a sample — check every one in the feature area)
- **Services:** all service classes in scope (orchestration + leaf services)
- **Repositories / data access:** ORM queries, raw SQL, stored proc calls
- **DI registration:** module files, startup, composition root
- **Validators:** request/response validation classes
- **Base classes / shared infrastructure:** base endpoints, filters, middleware, interceptors
- **Models / entities / DTOs:** domain objects, entity configs
- **Shared utilities:** extension methods, helpers, constants files
- **Build & config layer:** `*.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `appsettings.*.json`, `packages.lock.json`. These files are easy to skip because they don't hold business logic, but they're where framework drift, wildcard package versions, committed secrets, and central-package-management inconsistencies hide. Scan them as part of every backend audit, not just when something looks wrong.

### Frontend layers to cover:

- **Components:** page components AND child/shared components for the feature
- **Services / stores:** all services injected into the feature components
- **Guards:** route guards protecting the feature
- **Interceptors:** HTTP interceptors that affect the feature
- **Forms:** form builders, validators, form-related helpers
- **Base classes / shared utilities:** inherited component base classes, shared pipes, directives
- **Build & config layer:** `package.json` (scripts, deps, pinned versions), `tsconfig*.json` (aliases, strict flags), `eslint.config.*` / `.eslintrc.*` (suppressed rules), `angular.json` / `nx.json` / `project.json` (build targets, budgets), `environment.ts` / `environments/*.ts` (committed secrets, hardcoded URLs). These fall into the same trap as the backend config layer — easy to skip, high-leverage when audited.

### Scope strategy:

- **Large repos, narrow feature scope:** cover ALL layers within that feature (don't stop at 3-4 files per feature)
- **Large repos, broad scope:** pick 2-3 feature areas and cover ALL layers within each, rather than surface-reading 10+ features
- **Small repos:** cover everything

Be explicit about what you examined and what you skipped. If you skipped a layer, say why.

## Step 4: Audit the Code

Work through the code systematically using three layers of checks:

### Layer 1 — Repo Standards (highest priority)

Check every rule you extracted in Step 1. A violation of a documented project rule is always at least **High** severity because the team already decided it matters.

### Layer 2 — Universal Principles

Apply these to all code regardless of tech stack. For each finding, tag which principle(s) it violates:

**Memory Leaks & Resource Management** — tag: `[Memory Leak]`

- Unsubscribed event listeners, timers, or intervals
- Objects that hold references preventing GC
- Streams/connections/file handles not closed
- In Angular: RxJS subscriptions not cleaned up (missing `takeUntilDestroyed`, `async` pipe, or explicit `unsubscribe`)
- In .NET: `IDisposable` objects not wrapped in `using` or `Dispose()` not called

**SOLID Violations** — tag the specific principle:

- `[SRP]` Classes/services doing more than one thing (>200 lines is a smell, but look for conceptual mixing)
- `[OCP]` Switch/if-chains on type tags instead of polymorphism
- `[LSP]` Subclass that throws or no-ops for inherited methods
- `[ISP]` Interface with many unrelated methods, or forced empty implementations
- `[DIP]` Concrete class dependencies instead of abstractions; `new` inside business logic

**DRY Violations** — tag: `[DRY]`

- Duplicated logic across files (copy-pasted blocks, not just similar names)
- Identical API calls or data transforms in multiple places
- Constants or config values repeated inline

**YAGNI Violations** — tag: `[YAGNI]`

- Dead code: unused variables, methods, imports, components
- Commented-out code left in place
- Over-engineered abstractions with only one consumer
- Config flags or feature toggles that are always on/off

### Layer 3 — Tech Stack Best Practices

Apply the rules from the reference files you loaded in Step 2. Tag these with the technology name (e.g. `[.NET]`, `[Angular]`) so the reader can quickly filter.

## Step 4.5: Consistency Pass — Self-Review Before Reporting

Before you start writing the report, spend a deliberate moment re-scanning for high-value findings that checklist-driven audits commonly miss. The layered checklist above is good at catching patterns you know to look for, but a careful human reviewer with no checklist will still sometimes beat it — usually on findings that don't fit neatly into any layer. This step closes that gap.

Ask yourself these questions explicitly and go look if any answer is "I don't know":

1. **Secrets and credentials** — Have I opened every `appsettings.*.json`, `.env*`, `environment.ts`, and similar file in scope? Do any contain values that look like real API keys, JWTs, connection strings, or user passwords? (Values longer than 20 characters with mixed case + digits are suspect. JWTs start with `eyJ`. Real credentials should be flagged as Critical.)

2. **Build / package configuration** — Have I opened the csproj / package.json / Directory.Build.props / Directory.Packages.props / global.json? Are there any `Version="*"`, wildcards, or framework-version inconsistencies? Any DI registrations that are never consumed?

3. **Dead code at the system level** — Not just unused variables, but: are there feature folders that only contain a Module.cs with no endpoints? Routes registered to `PlaceholderComponent`? Interceptors implemented but never added to `withInterceptors(...)`? SignalR hubs registered but never mapped? (Grep the composition root for every registered service/interceptor/hub and verify the other end exists.)

4. **Auth & authorization** — Every mutation endpoint: does it check ownership or role? Every OAuth / token validation function: does it actually validate, or does it stub `return true`? Every `AllowAnonymous` attribute: is it intentional?

5. **"Placeholder" text in production code paths** — Grep the scope for strings like "TODO", "placeholder", "will be implemented", "NotImplemented", "example.com", "test@", "Guid.Empty". Each hit is a potential finding.

6. **Project-standard violations** — Re-open `CLAUDE.md` / `CONTRIBUTING.md`. Pick 3 rules at random and verify the scope respects them. (The `no-RxJS` rule is a classic example — frequently documented, frequently violated in interceptors.)

7. **Doc-vs-code drift** — Do any doc comments, README claims, or XML doc blocks contradict what the code actually does? (E.g., DbContext labeled "(Read-Only)" that also exposes `SaveChanges`.)

If any of these surface a finding you hadn't written down yet, add it. Severity-rate it honestly using the rubric below — several of these categories (secrets, auth stubs) are Critical by default.

This pass typically takes 5–15 minutes and usually surfaces 2–4 additional findings in a large repo. It's what separates a checklist-compliant audit from one a careful human would have written.

---

## Step 5: Produce the Report

Format findings as a structured markdown report. Use the exact template below.

---

## Tech Debt Report — `[scope]`

### Summary

| Severity    | Count |
| ----------- | ----- |
| 🔴 Critical | N     |
| 🟠 High     | N     |
| 🟡 Medium   | N     |
| 🔵 Low      | N     |

**Stack detected:** [technologies found]
**Scope examined:** [files/directories reviewed]

---

### Repo Standards Violations

[List each violation with: file path, which rule it breaks, recommended fix]

---

### Backend — [Technology] Issues

> **[SEVERITY] [Principle Tag(s)] — Finding Title**
>
> **File:** `path/to/file` (line N)
>
> What the problem is and why it matters.
>
> **Fix:** Concrete recommendation.

---

### Frontend — [Technology] Issues

[Same format]

---

### Cross-Cutting Issues

[Same format — issues that span both frontend and backend]

---

### Quick Wins

List the 3-5 highest-value fixes the team could do right now — things that are low effort with meaningful payoff.

### Needs Discussion

List any architectural concerns that require a team decision, not just a code fix.

---

## Step 6: Append Machine-Readable Findings

After the markdown report, append a JSON code block with all findings in a structured format. This enables downstream automation (auto-fixer, orchestrator) to parse and act on findings.

Use this exact schema:

```json
{
  "generatedAt": "ISO-8601 timestamp",
  "scope": "what was audited",
  "stack": ["detected technologies"],
  "findings": [
    {
      "id": "TD-001",
      "severity": "Critical|High|Medium|Low",
      "tags": ["Memory Leak", "Angular"],
      "title": "Short descriptive title",
      "file": "relative/path/to/file.ts",
      "line": 47,
      "description": "What the problem is and why it matters",
      "fix": "Concrete recommendation for how to fix it",
      "effort": "low|medium|high",
      "category": "tech-debt"
    }
  ],
  "summary": {
    "critical": 0,
    "high": 0,
    "medium": 0,
    "low": 0,
    "total": 0
  }
}
```

**ID format:** `TD-NNN` (tech debt), sequential starting at 001.

**Effort guide:**

- `low` — single file change, <20 lines, no architectural impact
- `medium` — 2-5 files, may require new abstractions or interface changes
- `high` — 5+ files, architectural change, requires team discussion

---

## Principle Tags Reference

| Tag                | Meaning                          |
| ------------------ | -------------------------------- |
| `[SRP]`            | Single Responsibility Principle  |
| `[OCP]`            | Open/Closed Principle            |
| `[LSP]`            | Liskov Substitution Principle    |
| `[ISP]`            | Interface Segregation Principle  |
| `[DIP]`            | Dependency Inversion Principle   |
| `[DRY]`            | Don't Repeat Yourself            |
| `[YAGNI]`          | You Aren't Gonna Need It         |
| `[Memory Leak]`    | Resource not released            |
| `[Performance]`    | Inefficiency at scale            |
| `[Security]`       | Potential vulnerability          |
| `[Repo Standards]` | Violates a project-specific rule |

A finding can carry multiple tags, e.g. `[DRY] [DIP]`.

## Severity Guide

Severity is the most-consumed field in your output — the orchestrator sorts on it, teams triage from it. Apply the rubric with examples in mind, not just the abstract definitions. When in doubt, err on the side of the higher severity for anything involving secrets, authentication, data integrity, or authorization.

- **Critical** — Active bugs, data loss risk, security vulnerabilities, or code that doesn't work correctly. Concrete examples that land here:
  - Secrets (real keys, passwords, tokens) committed to the repo in `appsettings.*.json`, `.env*`, or shipped in client bundles via `environment.ts`
  - Hardcoded user credentials in source (even "test" accounts become real if the backend URL is real)
  - Authentication/authorization checks that are stubbed, TODO'd, or pass trivially (e.g., `ValidateOAuthState` that always returns `true`)
  - Write operations that silently corrupt audit fields (`CreatedBy = Guid.Empty`)
  - Endpoints that mutate data without ownership checks when the model expects them
  - Timezone or currency bugs that silently produce wrong persisted values

- **High** — Violation of documented project standards, real resource leaks, or non-reproducible builds:
  - A rule explicitly documented in `CLAUDE.md` / `CONTRIBUTING.md` being violated (these are always at least High because the team already decided it matters)
  - Subscriptions / intervals / event listeners without teardown
  - Wildcard or floating package versions (`Version="*"`) — builds are not reproducible
  - Framework version drift across the solution (`Directory.Build.props` says one TFM, csproj files say another)
  - A service layer registered in DI but never consumed, especially when its signature has drifted from the endpoint duplicating its logic
  - Swallowed HTTP errors / empty catch blocks on the primary path

- **Medium** — Will cause problems at scale, makes future changes harder, or breaks a well-established pattern:
  - Heavy N+1 query patterns (problematic at scale, survivable at current size)
  - Fragile tests asserting on styling classes or private framework internals
  - Duplicated logic across 3+ files that should be extracted
  - God services / controllers (>300 lines, >7 dependencies)
  - Deprecated API usage with a well-known replacement

- **Low** — Code smell or polish that doesn't block anything:
  - Minor naming inconsistencies
  - Stale TODO comments (but not stale security TODOs — those are High or Critical)
  - Stylistic issues not covered by a linter

**If a finding could reasonably be placed in two tiers, document the reason briefly in the description and pick the higher tier.** Under-triaged Critical findings are the worst failure mode — a real security issue buried in a Low-severity list is worse than not finding it at all.

## Important Notes

- **Be specific.** Cite exact files and line numbers. Vague findings are not useful.
- **Skip false positives.** If a pattern looks like a violation but has a clear reason to exist, note the context and skip it.
- **Don't nitpick style.** If there's a linter enforcing it, don't report it — the tooling handles it.
- **Flag what you couldn't check.** If you didn't have access to certain directories or a check requires runtime behavior you can't observe statically, say so.
