---
description: Flip one or more backlog findings from pending to approved, making them eligible for /bob:raise and /bob:fix
argument-hint: <finding-id> [finding-id...]
---

Mark backlog findings as approved for fixing. This is a thin wrapper around editing `.bob/backlog.json` directly — it mutates the `status` field and saves.

## Step 1: Parse arguments

Arguments are one or more finding IDs (e.g., `TD-001`, `EF-003`, `CS-005`). Split the argument string on whitespace to get the list.

**If no IDs are provided**, read `.bob/backlog.json` and list the top 10 pending findings with `id`, `severity`, and `title`. Prompt: "Run `/bob:approve <id>` to approve, or `/bob:reject <id>` to remove." Do NOT mutate anything.

## Step 2: Load backlog

Read `.bob/backlog.json`. If it doesn't exist, tell the user to run `/bob:audit` first and stop.

## Step 3: Validate and flip each ID

For each ID in the argument list:

1. **Find the matching finding** in `queue`. Match on exact `id`.
2. **If not found**: print a warning (`Finding <id> not in backlog — skipping.`) and continue to the next ID. Don't abort.
3. **If found**:
   - If `status == "pending"` → set to `"approved"`. Print `✓ <id> approved: <title>`.
   - If `status == "approved"` → no-op, print `- <id> already approved`.
   - If `status == "fixing"` → warn and skip (`⚠ <id> is currently being processed by a fix run — not changing status`).
   - Any other status (shouldn't happen in normal flow): warn and skip with the current status noted.

## Step 4: Save

Write the updated `.bob/backlog.json` atomically (write to temp file + rename, if possible, to avoid corruption on crash).

## Step 5: Summary

Print a compact summary:

```
Approved: N findings
Skipped:  M (already approved, in-flight, or not found)

Next: run /bob:raise to file issues, or /bob:fix to start processing.
```

## Notes

- Approve is idempotent — re-approving an already-approved finding is a no-op.
- This command only touches `status`. It does NOT file issues or create PRs — that's `/bob:raise` and `/bob:fix`.
- Supports batching: `/bob:approve TD-001 TD-003 EF-002 CS-005` approves four findings in one call.
