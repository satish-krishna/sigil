---
name: enhancement-finder
description: >
  Use when the user asks to find enhancement opportunities, improve UX patterns, find missing
  features, check accessibility, audit API ergonomics, find user experience gaps, or asks
  "what could be better", "what's missing", "how can we improve UX", or "check accessibility".
  Also trigger when the quality-orchestrator dispatches an enhancement scan.
---

# Enhancement Finder

You are scanning for what's _missing_, not what's _broken_. Your job is to find gaps in UX patterns, accessibility, API design, and feature completeness that would improve the product for users and developers.

## Step 1: Discover Repo Standards

Read these files if they exist:

- `CLAUDE.md` — project conventions, stack info
- `CONTRIBUTING.md` — contribution guidelines
- Any design system docs or UX guidelines

Extract the tech stack, design patterns, and any documented UX requirements.

## Step 2: Detect the Tech Stack

Same as tech-debt-analyzer: scan `package.json`, `*.csproj`, `angular.json` to identify frontend and backend frameworks.

## Step 3: Determine Scope

Same minimum file coverage rules as tech-debt-analyzer: read at least 18-20 files. Cover all layers for the feature area being examined.

For enhancement-finding, pay special attention to:

- **Templates/views** — this is where UX gaps are visible
- **API response shapes** — this is where ergonomics matter
- **Route configs** — this is where navigation gaps appear

## Step 4: Scan for Enhancement Categories

### A. UX Pattern Gaps — tag: `[UX]`

- **Missing loading states**: async operations (Resource API, fetch calls, form submissions) without loading indicators in the template
- **Missing empty states**: lists/tables that render blank when data is empty instead of showing a helpful message
- **Missing error states**: async operations without error handling UI (no error message, no retry button)
- **Missing confirmation dialogs**: destructive actions (delete, cancel, abandon) that execute immediately without user confirmation
- **Missing optimistic updates**: mutations that wait for server response before updating UI, causing perceived lag
- **Missing form feedback**: form fields without inline validation messages, submit buttons without disabled state during submission
- **Missing toast/notification**: operations that complete silently with no user feedback

### B. Accessibility Gaps — tag: `[A11Y]`

- **Images without alt text**: `<img>` tags without `alt` attribute or with empty `alt=""`
- **Missing ARIA labels**: interactive elements (buttons, links, inputs) without accessible names
- **No keyboard navigation**: custom interactive elements (dropdowns, modals, tabs) without `tabindex`, `keydown`, or focus management
- **Color-only information**: status indicators that rely solely on color without text or icon alternatives
- **Missing focus management**: modals that don't trap focus, route changes that don't announce or focus
- **Missing skip links**: no skip-to-content link for keyboard users
- **Form inputs without labels**: inputs using placeholder as label instead of `<label>` element

### C. API Improvements — tag: `[API]`

- **Missing pagination**: list endpoints that return all records without page/size parameters
- **Missing input validation**: endpoints without request validators
- **Inconsistent response shapes**: some endpoints return `{ data: ... }`, others return raw objects
- **Wrong HTTP status codes**: POST returning 200 instead of 201, not-found returning 200 with null
- **Missing filtering/sorting**: list endpoints without query parameters for filtering or sorting
- **Missing caching headers**: static or infrequently-changing responses without Cache-Control headers
- **N+1 API calls**: frontend making separate API calls for data that could be a single request

### D. Missing Complementary Features — tag: `[Feature]`

- **Incomplete CRUD**: Create exists but no Update or Delete
- **Lists without search/filter**: list views with no way to narrow results
- **Forms without drafts**: long forms that lose data on navigation
- **Detail views without breadcrumbs**: nested pages without navigation context
- **Missing bulk operations**: repetitive single-item actions that could be batched
- **Missing export**: data-heavy views with no way to export/download

## Step 5: Produce the Report

Use the same report format as tech-debt-analyzer, but with enhancement-specific sections:

---

## Enhancement Report — `[scope]`

### Summary

| Severity    | Count |
| ----------- | ----- |
| 🔴 Critical | N     |
| 🟠 High     | N     |
| 🟡 Medium   | N     |
| 🔵 Low      | N     |

**Stack detected:** [technologies]
**Scope examined:** [files reviewed]

---

### UX Pattern Gaps

[findings with file, line, description, fix]

### Accessibility Gaps

[findings]

### API Improvements

[findings]

### Missing Features

[findings]

---

### Quick Wins

3-5 highest-value improvements that are low effort.

### Needs Discussion

Architectural or design decisions requiring team input.

---

## Step 6: Append Machine-Readable Findings

Same JSON contract as tech-debt-analyzer, but with `"category": "enhancement"` and IDs prefixed with `EF-`:

```json
{
  "generatedAt": "ISO-8601",
  "scope": "...",
  "stack": ["..."],
  "findings": [
    {
      "id": "EF-001",
      "severity": "High",
      "tags": ["UX"],
      "title": "Missing loading state on events list",
      "file": "path/to/component.ts",
      "line": 42,
      "description": "...",
      "fix": "...",
      "effort": "low",
      "category": "enhancement"
    }
  ],
  "summary": { "critical": 0, "high": 0, "medium": 0, "low": 0, "total": 0 }
}
```

## Severity Guide (Enhancement-specific)

- **Critical**: Accessibility violation that blocks users (missing form labels on critical flows, no keyboard nav on primary interactions)
- **High**: Missing UX pattern that causes user confusion (no loading state, no error feedback, silent failures)
- **Medium**: API inconsistency or missing feature that would improve DX/UX
- **Low**: Polish item, nice-to-have, or minor ergonomic improvement

## Important Notes

- **Focus on real user impact.** Don't flag theoretical accessibility issues — check the actual templates.
- **Respect existing patterns.** If the project consistently handles errors one way, don't flag missing error handling in a style they don't use.
- **Be concrete.** "Add loading state" is too vague. "Show a skeleton loader while `eventsResource.isLoading()` is true in the events-list template" is actionable.
