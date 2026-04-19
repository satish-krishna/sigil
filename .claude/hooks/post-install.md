# Bob — Post-Install Setup

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
    4. Filter: remove items already in state/history.json (pr-created, stale, or failed with active cooldown)
    5. Prioritize: Critical > High > Medium > Low, then low-effort first
    6. Write to state/backlog.json
    7. Print summary with counts by severity
```

### 2. Daily Fix (Tue-Fri 4 AM)

```
Create a scheduled task:
  taskId: bob-fix
  cron: "0 4 * * 2-5"
  description: "Daily auto-fix — picks top 3 findings from backlog, fixes with TDD, creates PRs"
  prompt: |
    Run the bob fix pipeline:
    1. Read state/backlog.json — if empty, output "No backlog" and stop
    2. Pick top 3 pending findings (skip failed items in cooldown, skip items with open PRs)
    3. For each finding sequentially:
       a. Invoke bob:auto-fixer with the finding
       b. Log result to state/history.json
       c. Return to target branch before next finding
    4. After all attempts, run final verification on target branch
    5. Print summary: PRs created, failures, remaining backlog
```

## Configuration

The plugin uses these defaults. Override by setting environment variables or modifying the scheduled task prompts:

| Setting            | Default         | Description                            |
| ------------------ | --------------- | -------------------------------------- |
| Max fixes per run  | 3               | Findings attempted per daily fix run   |
| Target branch      | `dev` or `main` | Base branch for PRs (auto-detected)    |
| Failure cooldown   | 7 days          | Time before retrying a failed finding  |
| Severity threshold | Medium          | Minimum severity to include in backlog |

## State Files

The plugin creates runtime state in `bob/state/`:

- `backlog.json` — prioritized queue of findings (refreshed weekly)
- `history.json` — log of all fix attempts (PRs, failures, stale items)

**Add to `.gitignore`:**

```
bob/state/backlog.json
bob/state/history.json
```

## Manual Commands

After installation, these commands are available:

- `/bob:audit [scope]` — run all 3 analyzers manually
- `/bob:fix [finding-id]` — fix a single finding with TDD
- `/bob:simplify [scope]` — find and simplify complex code
- `/bob:enhance [scope]` — find enhancement opportunities
