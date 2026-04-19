---
description: Find enhancement opportunities (UX, accessibility, API, missing features)
---

Find what's missing in the codebase — UX gaps, accessibility issues, API improvements, and incomplete features:

1. If a scope argument is provided (file or directory), analyze that scope
2. If no scope is given, analyze the full repo
3. Invoke the `bob:enhancement-finder` skill on the determined scope
4. Present findings grouped by category: UX Pattern Gaps, Accessibility, API Improvements, Missing Features
5. For each finding, ask the user: "Fix now?", "Skip?", or "Add to backlog?"
6. For "Fix now" items, invoke `bob:auto-fixer` for each sequentially
7. For "Add to backlog" items, append to `.bob/backlog.json`

For the enhancement analysis, follow the `bob:enhancement-finder` skill.
For fixing, follow the `bob:auto-fixer` skill.
