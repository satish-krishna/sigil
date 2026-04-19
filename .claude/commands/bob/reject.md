---
description: Remove one or more backlog findings and log them as rejected so future analyses don't re-surface them
argument-hint: <finding-id> [finding-id...] [--reason "explanation"]
---

Reject a backlog finding — the user has decided it's not tech debt in their context (intentional pattern, deferred scope, false positive, etc.). The finding is removed from `.bob/backlog.json` and logged in `.bob/history.json` as `status: "rejected"` with an optional reason. The quality-orchestrator's dedup filter checks history, so rejected findings won't come back on the next analysis unless the code changes enough to produce a different finding.

## Step 1: Parse arguments

Split the argument string into:
- One or more finding IDs (e.g., `TD-001`, `EF-003`)
- An optional `--reason "..."` flag that applies to all rejected IDs in this call

Example: `/bob:reject TD-002 TD-007 --reason "intentional god-class for perf — revisit if team grows"`.

**If no IDs are provided**, read the backlog and list top 10 pending findings (same as `/bob:approve` with no args). Do not mutate anything.

**If no `--reason` is provided**, use a default reason of `"rejected by user"`. Don't block on it — reject should be frictionless.

## Step 2: Load backlog and history

Read `.bob/backlog.json`. If missing, tell the user to run `/bob:audit` first and stop.

Read `.bob/history.json`. If missing, initialize it: `{"fixes": []}`.

## Step 3: Process each rejection

For each ID:

1. **Find the finding** in backlog `queue`. Match on exact `id`.
2. **If not found**: print `Finding <id> not in backlog — skipping.` and continue.
3. **If found**:
   - If `status == "fixing"` → warn and skip (`⚠ <id> is currently being processed — not rejecting`). The in-flight fix finishes on its own terms.
   - Otherwise:
     - **Remove** the finding from `queue` in backlog.
     - **Append** an entry to `history.json`:
       ```json
       {
         "findingId": "<id>",
         "title": "<finding title>",
         "file": "<finding file>",
         "status": "rejected",
         "reason": "<reason>",
         "timestamp": "<ISO-8601>"
       }
       ```
     - Print `✓ <id> rejected: <title>`.

## Step 4: Save both files

Write updated `.bob/backlog.json` and `.bob/history.json` atomically (temp file + rename if possible).

## Step 5: Summary

```
Rejected: N findings (logged to .bob/history.json)
Skipped:  M (in-flight or not found)
Reason:   "<reason used>"
```

## Notes

- Rejected findings are permanently filtered out of future analyses by the orchestrator's dedup step (`skills/quality-orchestrator/SKILL.md` Step 4). If you change your mind, delete the rejection entry from `.bob/history.json` and re-run `/bob:audit`.
- If the next analysis surfaces a similar-but-different finding (different file, different line, different title), the dedup logic won't match and the new finding WILL be re-surfaced as pending. That's intentional — rejection applies to the exact finding, not the concept.
- Reject does NOT close any GitHub issues that might have been filed. If you ran `/bob:raise` on this finding first, close the issue manually. (Rationale: reject is local state; closing an issue is a GitHub-side action that may have comments or context the user wants to preserve.)
- Batching: `/bob:reject TD-001 TD-004 EF-002 --reason "deferred to Q3"` rejects three findings with a shared reason.
