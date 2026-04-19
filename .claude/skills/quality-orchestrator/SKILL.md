---
name: quality-orchestrator
description: >
  Use as the scheduled entry point for autonomous code quality improvement. Chains analysis
  skills (tech-debt-analyzer, enhancement-finder, code-simplifier) with fix skills (auto-fixer,
  test-gap-filler) to find and fix issues automatically. Also trigger via `/bob:audit`.
---

# Quality Orchestrator

You are the control loop for autonomous code quality improvement. You dispatch analyzers, prioritize findings, and feed them to fixers — all without human intervention.

## Two Pipelines

### Pipeline 1: Analysis (Weekly — Monday)

Runs all three analysis skills, merges and prioritizes findings into a backlog.

### Pipeline 2: Fix (Daily — Tue-Fri)

Picks the top N findings from the backlog and dispatches auto-fixer for each.

---

## Pipeline 1: Analysis

### Step 1: Dispatch Analyzers in Parallel

Launch 3 agents simultaneously, each running one analyzer on the full repo:

1. **Agent 1:** Invoke `bob:tech-debt-analyzer` — full repo scope
2. **Agent 2:** Invoke `bob:enhancement-finder` — full repo scope
3. **Agent 3:** Invoke `bob:code-simplifier` — full repo scope

Each agent produces a markdown report with a JSON findings block at the end.

### Step 2: Collect and Parse Findings

From each agent's output, extract the JSON findings block (the ````json` block after "Machine-Readable Findings").

Merge all findings into a single list.

### Step 3: Deduplicate

If the same `file` + `line` (±5 lines) appears in multiple reports:

- Keep the finding with the highest severity
- Merge tags from all reports
- Note in description that multiple analyzers flagged this

### Step 4: Filter Already-Fixed

Read `.bob/history.json` (create if missing). Remove any finding where:

- A finding with matching `file` + `title` has `status: "pr-created"` and the PR is still open or merged
- A finding has `status: "failed"` and `cooldownUntil` is in the future (7-day cooldown)
- A finding has `status: "stale"` (code already changed)
- A finding has `status: "rejected"` (human explicitly rejected via `/bob:reject`) — these are permanently filtered unless the rejection entry is manually removed from `history.json`

### Step 5: Prioritize

Sort remaining findings by:

1. **Severity**: Critical > High > Medium > Low
2. **Within same severity**: `effort: "low"` first (quick wins)
3. **Within same effort**: `tech-debt` > `enhancement` > `simplification` (fix what's broken before adding what's missing)

### Step 6: Write Backlog

Write to `.bob/backlog.json`:

```json
{
  "generatedAt": "ISO-8601",
  "analyzedBy": ["tech-debt-analyzer", "enhancement-finder", "code-simplifier"],
  "totalFindings": 24,
  "queue": [
    {
      "id": "TD-001",
      "severity": "Critical",
      "tags": ["Security"],
      "title": "...",
      "file": "...",
      "line": 47,
      "description": "...",
      "fix": "...",
      "effort": "low",
      "category": "tech-debt",
      "status": "pending",
      "issueUrl": null
    }
  ]
}
```

**Status lifecycle (in-backlog values only — terminal outcomes move to history.json):**

- `"pending"` — default after analysis. Waiting for a human to review and approve.
- `"approved"` — human has flipped this to signal "yes, fix this." Eligible for `/bob:raise` (files the issue), `/bob:fix` with no arg (fixes top approved), and the scheduled fix pipeline.
- `"fixing"` — transient marker set by the caller while auto-fixer is processing. Used for crash recovery — if a fix run dies mid-finding, you can see which item was in flight.

**The `issueUrl` field** is optional. It's populated when `/bob:raise` files a GitHub issue ahead of the fix, and reused by auto-fixer (Step 9) to avoid creating duplicate issues. If `issueUrl` is null and the fix runs, auto-fixer files the issue inline as part of its normal flow.

Terminal outcomes (`pr-created`, `failed`, `stale`, `needs-discussion`) **do not** stay in backlog — the caller removes the item or resets it to `pending`, and records the outcome in `.bob/history.json`.

### Step 7: Output Summary

Print a summary to the user/log:

```
Quality Engine Analysis — [date]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
New findings:     24
Already fixed:     3
In cooldown:       2
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Backlog:          24 pending
  🔴 Critical:     2
  🟠 High:         5
  🟡 Medium:      12
  🔵 Low:          5
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Next fix run: Tomorrow 4 AM
```

---

## Pipeline 2: Fix

### Step 1: Read Backlog

Read `.bob/backlog.json`. If it doesn't exist or is empty → output "No backlog. Run analysis first." and stop.

### Step 2: Pick Top N Findings

Default: **N = 3** (configurable).

Select the top N items from the queue where **`status == "approved"`**. `"pending"` items are awaiting human approval and will NOT be picked up by this scheduled pipeline — that's the whole point of the approval gate. If the human never flips anything to `"approved"`, this pipeline does nothing on its scheduled run, which is a valid state.

Skip items that:

- Have a matching entry in `.bob/history.json` with `status: "failed"` and `cooldownUntil` in the future
- Have a matching open PR (check `.bob/history.json` for `status: "pr-created"`)

If fewer than N items are eligible → take what's available. If zero → output "No approved items in backlog. Flip findings to `\"status\": \"approved\"` in `.bob/backlog.json`, then (optionally) run `/bob:raise` to file issues, and let the next scheduled run pick them up." and stop.

### Step 3: Fix Sequentially

For each finding (one at a time, NOT parallel — they may touch overlapping files):

1. **Dispatch auto-fixer** with the finding as input
2. **Wait for completion**
3. **Read result:**
   - `pr-created` → log to `.bob/history.json`, mark as done in backlog
   - `stale` → log to history, remove from backlog
   - `failed` → log to history with 7-day cooldown, keep in backlog
   - `needs-discussion` → log to history, keep in backlog (won't be retried until cooldown expires)

4. **Before starting next finding**: ensure we're back on the target branch
   ```bash
   git checkout <target-branch>
   git pull origin <target-branch>
   ```

### Step 4: Post-Run Verification

After all fixes attempted, run a final verification on the target branch:

```bash
git checkout <target-branch>
# Run full test suite (commands depend on project)
```

This catches any state that might have leaked from failed fix attempts.

### Step 5: Update State

Write updated `.bob/history.json`:

```json
{
  "lastRunAt": "ISO-8601",
  "fixes": [
    {
      "findingId": "TD-001",
      "status": "pr-created",
      "prUrl": "https://github.com/...",
      "branch": "bob/tech-debt/TD-001",
      "timestamp": "ISO-8601",
      "testsAdded": ["..."],
      "filesChanged": ["..."]
    },
    {
      "findingId": "EF-003",
      "status": "failed",
      "error": "Test suite regression",
      "timestamp": "ISO-8601",
      "cooldownUntil": "ISO-8601 + 7 days"
    }
  ]
}
```

### Step 6: Output Summary

```
Quality Engine Fix Run — [date]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Attempted:    3
PRs created:  2
Failed:       1
Stale:        0
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
PRs:
  • fix(dashboard): clear interval on destroy
    → https://github.com/.../pull/42
  • feat(events): add loading state to event list
    → https://github.com/.../pull/43

Failed:
  • TD-005: VenueRepository case-sensitive search
    → Reason: regression in VenueSearchTests
    → Cooldown until: [date + 7 days]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Remaining backlog: 21 findings
Next run: Tomorrow 4 AM
```

---

## Scheduled Task Configuration

When first invoked, create two scheduled tasks:

**Analysis (Monday 3 AM):**

```
taskId: bob-analyze
cron: "0 3 * * 1"
description: "Weekly code quality analysis"
prompt: "Run the quality-orchestrator analysis pipeline (Pipeline 1). Invoke tech-debt-analyzer, enhancement-finder, and code-simplifier in parallel on the full repo. Merge findings, deduplicate, filter already-fixed items, prioritize, and write to .bob/backlog.json."
```

**Fix (Tue-Fri 4 AM):**

```
taskId: bob-fix
cron: "0 4 * * 2-5"
description: "Daily auto-fix from quality backlog"
prompt: "Run the quality-orchestrator fix pipeline (Pipeline 2). Pick top 3 findings from .bob/backlog.json where status == 'approved'. For each, dispatch auto-fixer with TDD. Create PRs for successful fixes. Log all results to .bob/history.json. If no items are approved, do nothing — that's expected on days when no human has triaged the backlog."
```

---

## Failure Handling

| Scenario                              | Action                                       |
| ------------------------------------- | -------------------------------------------- |
| Finding is stale (code changed)       | Mark `stale`, remove from backlog            |
| Cannot write test for finding         | Mark `needs-discussion`, 7-day cooldown      |
| Fix breaks existing tests (2 retries) | Mark `failed`, revert branch, 7-day cooldown |
| Git conflict on branch creation       | Mark `blocked`, skip, retry next run         |
| Build fails after fix                 | Same as "breaks tests"                       |
| Backlog is empty                      | No-op, output "backlog empty"                |
| All top N in cooldown                 | No-op, output "all in cooldown"              |

## Constraints

- **Never run fixers in parallel.** Sequential only — they may touch overlapping files.
- **3 fixes per daily run.** Conservative to keep runs under 30 minutes.
- **7-day failure cooldown.** Prevents hammering the same broken fix.
- **Weekly analysis refreshes context.** Monday re-analysis may produce better fix suggestions.
- **State files are the source of truth.** Always read before acting, always write after acting.
