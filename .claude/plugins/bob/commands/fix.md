---
description: Fix a quality finding with TDD and create a PR
---

Fix a single quality finding using test-driven development:

1. If a finding ID is provided as an argument (e.g., `TD-001`), look it up in `state/backlog.json`
2. If no finding ID is given, pick the highest-priority pending finding from the backlog
3. If no backlog exists, tell the user to run `/bob:audit` first
4. Invoke the `bob:auto-fixer` skill with the selected finding
5. Report the result: PR URL on success, or failure reason

The auto-fixer will:

- Validate the finding still exists
- Create an isolated branch
- Write a failing test (RED)
- Implement the minimal fix (GREEN)
- Verify no regression (full test suite + build)
- Create a PR with full context

For the complete workflow, follow the `bob:auto-fixer` skill.
