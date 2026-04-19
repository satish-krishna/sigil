---
name: tech-debt-analyzer
description: >
  Use when the user asks to find tech debt, review code quality, identify optimization
  opportunities, audit a codebase, check for code smells, find memory leaks, review architectural
  patterns, or asks "what needs cleanup", "what's wrong with this code", "review this for best
  practices", or "find issues in this repo/file/module". Also trigger when user mentions SOLID,
  DRY, YAGNI, subscription cleanup, memory management, or performance issues in the context of
  reviewing existing code.
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

### Frontend layers to cover:

- **Components:** page components AND child/shared components for the feature
- **Services / stores:** all services injected into the feature components
- **Guards:** route guards protecting the feature
- **Interceptors:** HTTP interceptors that affect the feature
- **Forms:** form builders, validators, form-related helpers
- **Base classes / shared utilities:** inherited component base classes, shared pipes, directives

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

- **Critical**: Active bugs, data loss risk, security issue, or the code doesn't work correctly
- **High**: Violation of documented project standards, or a real memory leak / resource exhaustion risk
- **Medium**: Will cause problems at scale, makes future changes harder, or breaks a well-established pattern
- **Low**: Code smell, minor duplication, or stylistic issue that doesn't block anything

## Important Notes

- **Be specific.** Cite exact files and line numbers. Vague findings are not useful.
- **Skip false positives.** If a pattern looks like a violation but has a clear reason to exist, note the context and skip it.
- **Don't nitpick style.** If there's a linter enforcing it, don't report it — the tooling handles it.
- **Flag what you couldn't check.** If you didn't have access to certain directories or a check requires runtime behavior you can't observe statically, say so.
