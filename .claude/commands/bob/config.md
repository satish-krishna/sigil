---
description: Configure bob settings for this repository (ticket format, labels)
---

Configure bob's per-repository settings. Settings are stored in `.bob/config.json` at the target repo root.

## Usage

`/bob:config [setting] [value]`

If invoked with no arguments, display the current configuration and prompt the user to choose what to change.

## Ticket Format

The primary setting is **ticket format** — whether bob creates a GitHub issue for each finding alongside the PR.

### Step 1: Read Current Config

Read `.bob/config.json`. If it doesn't exist, copy defaults from `${CLAUDE_PLUGIN_ROOT}/config/defaults.json` and use those as the starting point.

### Step 2: Show Current Settings

Display the current configuration:

```
Bob Configuration
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Ticket format:  <current value>
GitHub labels:  <labels>
Assign to author: <true|false>
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Step 3: Handle Arguments

**If arguments are provided**, apply them directly:

- `/bob:config ticket-format github-issue` → Create a GitHub issue for each finding alongside the PR (default)
- `/bob:config ticket-format pr-only` → Skip issue creation; only the fix PR is created
- `/bob:config github.labels bob,quality` → Set GitHub issue labels (comma-separated)
- `/bob:config github.assignToAuthor true` → Auto-assign issues to PR author

**If no arguments are provided**, ask the user interactively:

1. "How would you like bob to track findings?"
   - **GitHub Issue** (default) — Bob creates a GitHub issue for each finding and links the fix PR to it via `Closes #N`.
   - **PR only** — Bob creates only the fix PR. No separate issue.

2. If the user picks **GitHub Issue** (or keeps the default), ask:
   - Labels to apply (default: `bob, auto-fix`)
   - Whether to auto-assign issues to the PR author

### Step 4: Write Config

Write the updated config to `.bob/config.json`:

```json
{
  "ticketFormat": "github-issue",
  "github": {
    "labels": ["bob", "auto-fix"],
    "assignToAuthor": false
  }
}
```

Allowed values for `ticketFormat`: `"github-issue"` (default), `"pr-only"`.

### Step 5: Confirm

Display the updated configuration and confirm it was saved.

## Supported Ticket Formats

| Format | Description | Requirements |
|--------|-------------|--------------|
| `github-issue` | **Default.** Bob creates a GitHub issue per finding, then links the fix PR via `Closes #N`. | `gh` CLI authenticated |
| `pr-only` | Bob creates a fix PR only. No separate issue. | None |

## Notes

- Config is stored per repository in `.bob/config.json` (commit this — team-shared)
- The `${CLAUDE_PLUGIN_ROOT}/config/defaults.json` file provides the fallback defaults
- Config changes take effect on the next `/bob:fix` or scheduled fix run
- The auto-fixer reads this config to decide whether to create an issue alongside the PR
