# Bob — Post-Install Setup

> **Note:** This file is documentation, not a Claude Code hook. Claude Code hooks are JSON configurations in `hooks/hooks.json`. Bob's automation is driven by scheduled tasks (see below) rather than event-triggered hooks.

## Scheduled Tasks

After installing the bob plugin, set up the two scheduled tasks:

### 1. Weekly Analysis (Monday 3 AM)

```
Create a scheduled task:
  taskId: bob-analyze
  cron: "0 3 * * 1"
  description: "Weekly code quality analysis — scans for tech debt, enhancements, and simplification opportunities"
  prompt: |
    Run the bob analysis pipeline:
    1. Dispatch 3 parallel agents:
       - Agent 1: Run bob:tech-debt-analyzer on the full repo
       - Agent 2: Run bob:enhancement-finder on the full repo
       - Agent 3: Run bob:code-simplifier on the full repo
    2. Collect all findings from the JSON blocks in each report
    3. Deduplicate: same file+line (±5 lines) → keep highest severity
    4. Filter: remove items already in .bob/history.json (pr-created, stale, or failed with active cooldown)
    5. Prioritize: Critical > High > Medium > Low, then low-effort first
    6. Write to .bob/backlog.json
    7. Print summary with counts by severity
```

### 2. Daily Fix (Tue-Fri 4 AM) — approval-gated

```
Create a scheduled task:
  taskId: bob-fix
  cron: "0 4 * * 2-5"
  description: "Daily auto-fix — picks top 3 APPROVED findings, fixes with TDD, creates PRs"
  prompt: |
    Run the bob fix pipeline:
    1. Read .bob/backlog.json — if empty, output "No backlog" and stop
    2. Pick top 3 findings where status == "approved" (skip failed items in cooldown, skip items with open PRs). If no approved items, stop — that's expected on days with no triage.
    3. For each finding sequentially:
       a. Set status: "fixing" in backlog
       b. Invoke bob:auto-fixer with the finding
       c. On pr-created → remove item from backlog, log to .bob/history.json
          On failed → reset to status: "pending", log with 7-day cooldown
          On stale → remove from backlog, log to history
       d. Return to target branch before next finding
    4. After all attempts, run final verification on target branch
    5. Print summary: PRs created, failures, remaining backlog (approved + pending counts)
```

**Important:** the scheduled fix run is approval-gated by default. If no human has flipped items from `"pending"` to `"approved"` in `.bob/backlog.json`, the scheduled run does nothing. This is the intended behavior — autonomous fixing only happens on findings a human has signed off on.

## Configuration

### Ticket Format

Run `/bob:config` to choose how bob tracks findings. Two options:

| Format | Description | Requirements |
|--------|-------------|--------------|
| `github-issue` | **Default.** Creates a GitHub issue per finding, links the fix PR via `Closes #N`. | `gh` CLI authenticated |
| `pr-only` | Fix PR only, no separate issue. | None |

Quick setup:
```
/bob:config ticket-format pr-only
/bob:config github.labels bob,quality
/bob:config github.assignToAuthor true
```

Config is stored in `.bob/config.json` at the target repo root. Defaults come from `${CLAUDE_PLUGIN_ROOT}/config/defaults.json` (plugin-shipped).

### General Settings

The plugin uses these defaults. Override by setting environment variables or modifying the scheduled task prompts:

| Setting            | Default         | Description                            |
| ------------------ | --------------- | -------------------------------------- |
| Max fixes per run  | 3               | Findings attempted per daily fix run   |
| Target branch      | `dev` or `main` | Base branch for PRs (auto-detected)    |
| Failure cooldown   | 7 days          | Time before retrying a failed finding  |
| Severity threshold | Medium          | Minimum severity to include in backlog |

## State Files

The plugin creates runtime state in `.bob/` at the **target repo root** (not the plugin directory):

- `.bob/backlog.json` — prioritized queue of findings (refreshed weekly)
- `.bob/history.json` — log of all fix attempts (PRs, failures, stale items)
- `.bob/config.json` — per-repo configuration (created by `/bob:config`)
- `.bob/report.md` — optional markdown report written by `/bob:report`

### Recommended .gitignore for the target repo

Add to your repo's `.gitignore`:

```
# Bob runtime state — per-developer, except config.json which is team-shared
.bob/backlog.json
.bob/history.json
.bob/report.md
```

Leave `.bob/config.json` tracked so the team shares ticket format settings.


## Approval workflow

After `/bob:audit`, every finding in `.bob/backlog.json` starts with `"status": "pending"`. Bob will not create fix PRs for pending items — you must flip them to `"approved"` first. This gives you a triage step between "bob finds stuff" and "bob opens PRs against your repo."

**Typical weekly loop:**

1. Monday morning — Bob's scheduled analysis (or your `/bob:audit`) produces a prioritized backlog of pending findings.
2. Triage. Read through `.bob/backlog.json` and decide per finding:
   - Approve the ones you want fixed: `/bob:approve TD-001 TD-003 EF-002` (batched).
   - Reject the ones you don't: `/bob:reject TD-005 --reason "intentional pattern"`. Rejected findings are logged to `.bob/history.json` and won't come back on future analyses unless you clear the rejection.
   - (Or hand-edit `.bob/backlog.json` if you prefer. The commands are a convenience; the file is the source of truth.)
3. **(Optional)** Run `/bob:raise` to file a GitHub issue for each approved finding — no PRs yet. Useful if you want to assign issues to teammates, discuss in comments, or just have the work visible as GitHub issues before any code lands.
4. Wait for the Tue-Fri scheduled fix cron, OR call `/bob:fix` (processes top approved), OR `/bob:fix <id>` for a specific finding. Each creates a PR linked to the existing issue via `Closes #N` (or files a new issue if `/bob:raise` wasn't run).
5. Review the resulting PRs and merge. Merging auto-closes the issue.

**Fast path:** skip step 3 — `/bob:fix` creates the issue and PR atomically. Only use `/bob:raise` when you want the "file tickets first, fix later" workflow.

## Manual Commands

After installation, these commands are available:

- `/bob:audit [scope]` — run all 3 analyzers manually
- `/bob:approve <id>...` — flip one or more pending findings to approved
- `/bob:reject <id>... [--reason "..."]` — remove findings from backlog and prevent future re-surfacing
- `/bob:raise [--dry-run]` — file GitHub issues for every approved finding (no PRs)
- `/bob:fix [finding-id]` — fix one finding. No arg → top approved. With id → bypasses approval gate. Reuses the issue filed by `/bob:raise` if present.
- `/bob:simplify [scope]` — find and simplify complex code
- `/bob:enhance [scope]` — find enhancement opportunities
- `/bob:config [setting] [value]` — configure ticket format and other settings
- `/bob:report [format]` — generate a markdown report of fix history and backlog
