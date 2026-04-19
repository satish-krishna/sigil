---
description: File a GitHub issue for every approved backlog finding that doesn't already have one — no PRs
argument-hint: [--dry-run]
---

Create GitHub issues for every finding in `.bob/backlog.json` where `status == "approved"` and no `issueUrl` exists yet. This is the "file the paperwork" step — PRs come later via `/bob:fix` or the scheduled fix run.

Use this when you want the approved findings visible as issues before fix PRs land (team visibility, assignment workflows, public roadmap). If you'd rather create issue + PR atomically, skip this and go straight to `/bob:fix`.

## Step 1: Read the backlog

Read `.bob/backlog.json`. Filter the `queue` to findings where:

- `status == "approved"`, AND
- `issueUrl` is missing or empty

If nothing matches, output:

> No approved findings awaiting issue creation. Flip items to `"status": "approved"` in `.bob/backlog.json` first. (If every approved item already has an `issueUrl`, you're good — run `/bob:fix` next.)

...and stop.

## Step 2: Dry-run (optional)

If `--dry-run` was passed, print a table of each candidate's `id`, `severity`, `title`, and `file`. Do NOT call `gh`. Stop.

## Step 3: Read config

Read `.bob/config.json` (fall back to `${CLAUDE_PLUGIN_ROOT}/config/defaults.json`).

If `ticketFormat != "github-issue"`, output:

> Issue creation requires `ticketFormat: "github-issue"`. Current setting: <value>. Run `/bob:config ticket-format github-issue` to enable, or use `/bob:fix` directly if you prefer PR-only mode.

...and stop.

## Step 4: Create issues sequentially

For each candidate finding, one at a time (order preserved by backlog priority):

1. **Build the issue body** using the same template as `bob:auto-fixer` Step 9:

   ```
   ## Quality Finding

   **ID:** <finding-id>
   **Severity:** <severity>
   **Category:** <category>
   **Tags:** <tags>

   ### Problem
   <description>

   ### File
   `<file>` (line <line>)

   ### Suggested Fix
   <fix>

   ---
   🤖 Created by Bob quality engine
   ```

2. **Call `gh issue create`:**

   ```bash
   gh issue create \
     --title "[Bob] <finding-id>: <finding title>" \
     --label <comma-separated labels from config.github.labels> \
     --body "<body from step 1>"
   ```

3. **Capture the issue URL** returned by `gh`. Write it into the finding's `issueUrl` field in `.bob/backlog.json`.

4. **Save `.bob/backlog.json` immediately** after each issue — if a later call fails, the issues already created aren't lost.

5. **If `config.github.assignToAuthor` is `true`**, assign the issue to the authenticated user:

   ```bash
   gh issue edit <issue-url> --add-assignee @me
   ```

6. **On `gh issue create` failure** (auth error, network, rate limit): log a warning, leave the finding without an `issueUrl`, and continue to the next candidate. Do not abort the whole run — partial progress is valuable.

## Step 5: Summary

```
Issue-creation run — [timestamp]
━━━━━━━━━━━━━━━━━━━━━━━━━━━
Approved findings scanned:   N
  ✓ New issues created:      M
  - Already had issueUrl:    K  (skipped)
  ✗ Failed to create:        L  (will retry on next /bob:raise)
━━━━━━━━━━━━━━━━━━━━━━━━━━━
Approved findings now ready for /bob:fix: M + K
```

## Notes

- Raise is safe to run repeatedly — it skips items that already have an `issueUrl`.
- Raise only touches `approved` findings. Pending items need human sign-off first.
- Issues created here outlive fix attempts. If a fix later fails, the issue stays open so a human can handle it manually.
- Requires `ticketFormat: "github-issue"` (the default). For `pr-only` mode, raise does nothing — use `/bob:fix` directly.
