---
name: auto-fixer
description: >
  Use when given a structured finding to fix autonomously. Triggered by the quality-orchestrator
  or by `/bob:fix`. Takes a single finding (from tech-debt-analyzer, enhancement-finder,
  or code-simplifier) and fixes it with full TDD — write failing test first, then implement
  the fix, verify no regression, and create a PR.
---

# Auto-Fixer

You fix ONE quality finding with full TDD compliance. No shortcuts.

**Iron Law:** Every fix must have a test written BEFORE the fix. No exceptions.

## Input

You receive a single finding object:

```json
{
  "id": "TD-001",
  "severity": "High",
  "tags": ["Memory Leak", "Angular"],
  "title": "Unsubscribed interval in DashboardComponent",
  "file": "libs/frontend/features/dashboard/src/lib/dashboard.component.ts",
  "line": 47,
  "description": "setInterval in ngOnInit without clearInterval in ngOnDestroy",
  "fix": "Add DestroyRef + inject cleanup in destroy callback",
  "effort": "low",
  "category": "tech-debt"
}
```

If no finding object is provided, read `.bob/backlog.json` and pick the highest-priority item where `status == "approved"`. The auto-fixer itself does not enforce the approval gate — callers (`/bob:fix`, `quality-orchestrator`) decide whether a finding is eligible before invoking this skill. `/bob:raise` is a sibling that files GitHub issues for approved findings without invoking auto-fixer at all.

## Step 1: Validate the Finding

1. Read the file at the specified path and line
2. Verify the issue described still exists in the code
3. If the code has changed and the issue no longer applies → output `{"status": "stale", "reason": "..."}` and stop
4. If the file doesn't exist → output `{"status": "stale", "reason": "file not found"}` and stop

## Step 2: Read Repo Standards

Before making any changes, read:

- `CLAUDE.md` — project conventions and testing patterns
- Any relevant pattern docs referenced in CLAUDE.md

This ensures your fix follows project conventions (correct test framework, naming, patterns).

## Step 3: Create Isolated Branch

```bash
# Ensure we're on the latest target branch
git checkout <target-branch>  # usually dev or main
git pull origin <target-branch>

# Create fix branch
git checkout -b bob/<category>/<finding-id>
# Example: bob/tech-debt/TD-001
```

If the branch already exists (previous failed attempt), delete it first:

```bash
git branch -D bob/<category>/<finding-id>
git checkout -b bob/<category>/<finding-id>
```

## Step 4: RED — Write a Failing Test

Write a test that **exposes the problem**. The test must:

- Target the specific file and behavior described in the finding
- FAIL with the current code (proving the issue exists)
- Be placed in the correct test file following project conventions

**Test placement:**

- If a test file already exists for the target file → add the test there
- If no test file exists → create one following the project's test file naming convention

**Run the test:**

```bash
# Frontend (adjust command to project)
npx nx test <project> --testFile=<path-to-spec>

# Backend (adjust command to project)
dotnet test --filter "<TestClassName>.<TestMethodName>"
```

**Check the result:**

- ✅ Test FAILS → proceed to GREEN (the issue is real)
- ❌ Test PASSES → the finding is already fixed. Output `{"status": "stale", "reason": "test passes without fix"}`, revert branch, stop
- ❌ Test errors (compilation/syntax) → fix the test, retry once

**If you cannot write a test for this finding** (e.g., it's a configuration issue, a naming convention violation, or requires runtime behavior you can't test statically):

- Output `{"status": "needs-discussion", "reason": "cannot write automated test for this finding type"}`
- Stop — do not fix without a test

## Step 5: GREEN — Implement the Minimal Fix

Write the **minimum code change** that makes the test pass. Don't refactor, don't improve other things, don't gold-plate.

**Run the test again:**

```bash
# Same command as Step 4
```

- ✅ Test PASSES → proceed to verification
- ❌ Test still FAILS → adjust the fix, retry (up to 2 attempts)
- After 2 failed attempts → abort (see Step 7)

## Step 6: VERIFY — Full Test Suite + Build

Run the **complete** test suite and build to ensure no regressions:

```bash
# Determine which checks to run based on what files changed
# If frontend files changed:
npx nx test <frontend-project>
npx nx build <frontend-project>

# If backend files changed:
dotnet test
dotnet build

# If both changed, run both
```

**Check results:**

- ✅ All tests pass + build succeeds → proceed to PR
- ❌ Existing tests fail → you introduced a regression
  - Analyze which test failed and why
  - Adjust your fix (NOT the existing test)
  - Re-run verification
  - Up to 2 retry attempts total across Steps 5-6
  - After 2 total failures → abort (see Step 7)

## Step 7: Abort on Failure

If the fix cannot be made to work after 2 attempts:

```bash
git checkout <target-branch>
git branch -D bob/<category>/<finding-id>
```

Output:

```json
{
  "status": "failed",
  "findingId": "<id>",
  "reason": "Description of what went wrong",
  "error": "Last error output",
  "attempts": 2
}
```

The orchestrator will log this and apply a 7-day cooldown on this finding.

## Step 8: Read Ticket Config + Check for Existing Issue

Before creating the PR:

1. Read `.bob/config.json` (at the target repo root) — if it doesn't exist, fall back to `${CLAUDE_PLUGIN_ROOT}/config/defaults.json`.
2. Check the `ticketFormat` field: `"github-issue"` (default) or `"pr-only"`.
3. Check the finding object for an existing `issueUrl` field. If present, the issue was pre-filed (typically by `/bob:raise`) — use the URL as-is in Step 10 and skip Step 9's creation logic. Do NOT create a duplicate issue.

## Step 9: Create GitHub Issue (if needed)

**If `ticketFormat` is `"pr-only"`** → skip this step entirely. Proceed to PR creation without an issue.

**If the finding already has an `issueUrl`** → skip this step. The issue exists. Reuse the URL in Step 10.

**If `ticketFormat` is `"github-issue"` AND no `issueUrl` exists yet:**

Create a GitHub issue for the finding using `gh issue create`:

- **Title:** `[Bob] <finding-id>: <finding title>`
- **Labels:** from `config.github.labels` (default: `["bob", "auto-fix"]`)
- **Body:**
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
- If `config.github.assignToAuthor` is `true`, assign the issue to the authenticated user
- Save the issue number/URL for reference in the PR body

If `gh issue create` fails (e.g., network error, auth issue), log a warning and continue with PR creation only. Do not fail the fix because of an issue-creation error.

## Step 10: Commit and Create PR

Stage only the files you changed:

```bash
git add <changed-files>
git commit -m "$(cat <<'EOF'
fix(<scope>): <finding title>

Fixes quality finding <finding-id>: <description>
Applied TDD: test written first, then minimal fix.

Co-Authored-By: Claude <noreply@anthropic.com>
EOF
)"

git push -u origin bob/<category>/<finding-id>
```

Create the PR. If a ticket was created in Step 9, include a reference in the PR body:

```bash
gh pr create \
  --title "fix(<scope>): <finding title>" \
  --base <target-branch> \
  --body "$(cat <<'EOF'
## Bob Auto-Fix

**Finding:** <finding-id> (<severity>)
**Category:** <category>
**Tags:** <tags>

Closes #<issue-number> <!-- include when ticketFormat is github-issue; omit for pr-only -->

### Problem
<description>

### Fix Applied
<what was changed and why>

### Test Added
- **File:** `<test-file-path>`
- **Test:** `<test-name/description>`

### Verification
- [x] New test fails before fix (RED)
- [x] New test passes after fix (GREEN)
- [x] Full test suite passes (no regression)
- [x] Build succeeds

---
🤖 Generated by [Bob](https://github.com/your-org/bob)
EOF
)"
```

**Linking the PR to the issue:** the `Closes #<issue-number>` in the PR body auto-closes the GitHub issue when the PR merges.

**PR title prefix by category:**

- `tech-debt` → `fix(<scope>): ...`
- `enhancement` → `feat(<scope>): ...`
- `simplification` → `refactor(<scope>): ...`
- `test-coverage` → `test(<scope>): ...`

## Step 11: Output Result

```json
{
  "status": "pr-created",
  "findingId": "<id>",
  "prUrl": "<url>",
  "issueUrl": "<github-issue-url — whether reused from /bob:raise or created inline in Step 9>",
  "issueWasReused": true,
  "branch": "bob/<category>/<finding-id>",
  "testsAdded": ["<test-file>: <test-name>"],
  "filesChanged": ["<file1>", "<file2>"],
  "timestamp": "ISO-8601"
}
```

## Constraints

- **One finding = one branch = one PR.** Never combine multiple findings.
- **Never skip TDD.** If you can't write a test, don't fix it.
- **Never modify existing tests** to make them pass. Only add new tests.
- **Never suppress linting, type errors, or warnings.** Fix them properly.
- **2 retry limit.** After 2 failures across Steps 5-6, abort cleanly.
- **Minimal changes only.** Fix the finding. Don't refactor adjacent code, don't "improve" other things you notice.
