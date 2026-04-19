---
name: code-simplifier
description: >
  Use when the user asks to simplify code, reduce complexity, refactor for readability, find
  overly complex implementations, or when they say "simplify this", "this is too complex",
  "clean this up", "reduce complexity", or "make this more readable". Also trigger when
  the quality-orchestrator dispatches a simplification scan.
---

# Code Simplifier

You are looking for code that is more complex than it needs to be. Your job is to find concrete simplifications that reduce line count, nesting depth, or cognitive load without changing behavior.

**Key constraint:** Only flag genuine complexity. Code that is long but clear is NOT a finding. A 3-line explicit version beats a 1-line clever version.

## Step 1: Discover Repo Standards

Read `CLAUDE.md`, `CONTRIBUTING.md`, and any style guides. Extract conventions — patterns that look "complex" may be the project's intentional style.

## Step 2: Detect Tech Stack

Same as tech-debt-analyzer. Load relevant reference files if available.

## Step 3: Determine Scope

If scope is given, analyze that file/directory. If no scope, analyze **recently changed files** first (`git log --name-only -20` to find active code), then expand to core services and utilities.

Read at least 15-20 files before forming conclusions.

## Step 4: Identify Complexity Targets

### A. Cyclomatic Complexity — tag: `[Complexity]`

- Methods with more than **3 levels of nesting** (if inside if inside if)
- Methods with more than **4 conditional branches** (if/else if/else if/else)
- Switch/match statements with more than **5 cases**
- Boolean expressions with more than **3 operators** (`a && b || c && !d`)

### B. Length Violations — tag: `[Length]`

- Files over **300 lines**
- Methods/functions over **50 lines**
- Components with more than **5 injected dependencies** (likely doing too much)
- Template files over **200 lines** without extraction to child components

### C. Abstraction Overhead — tag: `[Overengineered]`

- Wrapper classes that add no behavior (pure delegation)
- Interfaces with only one implementation **that are not used for DI or testing**
- Factory patterns that create only one type
- Strategy patterns with only one strategy
- Builder patterns for objects with fewer than 4 fields
- Base classes used by only one subclass

### D. Simplification Opportunities — tag: `[Simplify]`

**Language-level simplifications:**

- Manual null checks → null-conditional operators (`?.`, `??`)
- Verbose LINQ chains → simpler expressions (`Where(x => x.Id == id).FirstOrDefault()` → `FirstOrDefault(x => x.Id == id)`)
- String concatenation in loops → `StringBuilder` or `string.Join`
- Manual `try/catch` just to rethrow with the same message
- `if (condition) return true; else return false;` → `return condition;`

**Framework-level simplifications:**

- Template logic that should be a `computed` signal
- Repeated template blocks → extract to child component
- Manual HTTP error handling that duplicates interceptor behavior
- Manual state management (loading/error/data signals) → `resource()` API
- Manual form validation → framework validators

### E. Duplication That Could Be Extracted — tag: `[Extract]`

- 3+ lines of identical code appearing in 2+ files → extract to shared utility
- Similar-but-different code blocks → parameterize the difference
- Copy-pasted test setup → extract to test helper

## Step 5: Produce the Report

For each finding, show:

---

## Simplification Report — `[scope]`

### Summary

| Severity  | Count |
| --------- | ----- |
| 🟠 High   | N     |
| 🟡 Medium | N     |
| 🔵 Low    | N     |

**Scope examined:** [files reviewed]
**Estimated total line reduction:** ~N lines

---

### Findings

> **[SEVERITY] [Tag] — Finding Title**
>
> **File:** `path/to/file` (lines N-M)
>
> **Current:** Brief description of what the code does and why it's complex.
>
> **Simpler:** Concrete description of the simpler alternative.
>
> **Line reduction:** ~N lines removed/simplified
>
> **Effort:** low | medium | high

---

### Quick Wins

Top 3-5 simplifications with highest line-reduction-to-effort ratio.

---

## Step 6: Append Machine-Readable Findings

Same JSON contract, `"category": "simplification"`, IDs prefixed with `CS-`:

```json
{
  "generatedAt": "ISO-8601",
  "scope": "...",
  "findings": [
    {
      "id": "CS-001",
      "severity": "Medium",
      "tags": ["Complexity"],
      "title": "Deeply nested conditional in processEvent",
      "file": "path/to/file.ts",
      "line": 47,
      "description": "3 levels of nesting with 5 branches",
      "fix": "Extract inner conditions to early-return guard clauses",
      "effort": "low",
      "category": "simplification",
      "lineReduction": 12
    }
  ],
  "summary": { "high": 0, "medium": 0, "low": 0, "total": 0 }
}
```

## Severity Guide

- **High**: Complexity that actively causes bugs or makes the code unmaintainable (>5 nesting levels, >100-line methods, god classes)
- **Medium**: Complexity that slows down future development (unnecessary abstractions, verbose patterns with simpler alternatives)
- **Low**: Minor simplification that would improve readability but isn't blocking

## Important Notes

- **Never simplify at the cost of readability.** If the "simpler" version is harder to understand, it's not simpler.
- **Respect project conventions.** `inject()` pattern in Angular is correct, not overengineered. Interfaces for DI are correct.
- **Don't flag test code.** Test setup is often verbose — that's fine.
- **Show, don't just tell.** For each finding, describe what the simpler version looks like specifically.
