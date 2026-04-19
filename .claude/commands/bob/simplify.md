---
description: Find and simplify overly complex code
---

Find complexity in the codebase and simplify it:

1. If a scope argument is provided (file or directory), analyze that scope
2. If no scope is given, analyze recently changed files: `git log --name-only -20 --pretty=format:""` to find active code
3. Invoke the `bob:code-simplifier` skill on the determined scope
4. Present findings to the user
5. For each finding, ask the user: "Fix now?", "Skip?", or "Add to backlog?"
6. For "Fix now" items, invoke `bob:auto-fixer` for each sequentially
7. For "Add to backlog" items, append to `.bob/backlog.json`

For the code-simplifier analysis, follow the `bob:code-simplifier` skill.
For fixing, follow the `bob:auto-fixer` skill.
