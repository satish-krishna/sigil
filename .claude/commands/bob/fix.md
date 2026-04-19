---
description: Fix one quality finding with TDD and create a PR (reuses an existing issue if already filed by /bob:raise)
argument-hint: [finding-id]
---

Fix one quality finding using test-driven development. Use this for a single item. For batch processing every approved finding, file issues first with `/bob:raise`, then let the scheduled fix cron drain the queue.

## Step 1: Select the finding

1. **If a finding ID is provided as an argument** (e.g., `TD-001`), look it up in `.bob/backlog.json`. Passing an ID bypasses the approval gate — the argument itself is a direct human request, so `status: "pending"` findings are fair game.

2. **If no ID is provided**, pick the highest-priority item from `.bob/backlog.json` where `status == "approved"`. This respects the approval gate.

   If no items are approved, output:

   > No approved items. Either flip a finding to `"status": "approved"` in `.bob/backlog.json` and re-run, or pass a specific finding ID (e.g., `/bob:fix TD-003`) to bypass approval.

   ...and stop.

3. **If no backlog exists**, tell the user to run `/bob:audit` first.

## Step 2: Check for an existing issue

Look at the selected finding's `issueUrl` field in `.bob/backlog.json`.

- **If `issueUrl` is present** (from a prior `/bob:raise`): the GitHub issue already exists. `auto-fixer` must reuse it — don't file a duplicate. Pass the `issueUrl` into the finding object before invoking auto-fixer so Step 9 of auto-fixer knows to skip issue creation.
- **If `issueUrl` is absent**: auto-fixer will create the issue inline as part of its normal flow (Step 9 creates, Step 10 links PR to it). This is the atomic path — useful for ad-hoc fixes on findings that were never raised.

## Step 3: Mark in-flight

Set `status: "fixing"` on the selected finding in `.bob/backlog.json` and save. If the run crashes or is interrupted, this tells you which item was mid-flight.

## Step 4: Invoke the auto-fixer

Invoke `bob:auto-fixer` with the finding. The fixer runs: validate → branch → RED → GREEN → VERIFY → (create or reuse issue) → create PR with `Closes #N`.

## Step 5: Record the outcome

Based on the auto-fixer's result:

- `pr-created` → **remove** the item from `.bob/backlog.json`'s `queue`. Append a fix entry to `.bob/history.json` with the PR URL, the issue URL, branch name, tests added, and files changed.
- `failed` → reset the item to `status: "pending"` in backlog (keep the `issueUrl` if present — the issue is still valid). Log a failed entry to history with `cooldownUntil = now + 7 days`.
- `stale` → remove from backlog, log stale entry to history. If the item had an `issueUrl`, leave a comment on the issue noting that the finding is stale and the issue can be closed.
- `needs-discussion` → reset to `status: "pending"` with a note in the finding's description. Log to history.

## Step 6: Report

Output the PR URL on success, or the failure reason + cooldown-until date on failure. Keep the summary to one sentence per outcome.

## Notes

- `/bob:fix` (no arg) respects the approval gate; `/bob:fix TD-001` bypasses it.
- If an issue was pre-filed via `/bob:raise`, this command reuses it — you get exactly one issue and one PR per finding.
- For batch processing, approve several findings, run `/bob:raise` once to file all the issues, then let the scheduled fix run process them over the week (3/day). Or call `/bob:fix` repeatedly for serial processing.
