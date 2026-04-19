---
description: Run a full code quality analysis (tech debt + enhancements + simplification)
---

Run the bob analysis pipeline:

1. Invoke the `bob:tech-debt-analyzer` skill on the scope provided (or full repo if no scope given)
2. Invoke the `bob:enhancement-finder` skill on the same scope
3. Invoke the `bob:code-simplifier` skill on the same scope
4. Combine all findings, deduplicate (same file+line → keep highest severity), and prioritize
5. Write results to `state/backlog.json` in the bob plugin directory
6. Present the combined report to the user with a summary table

If a scope argument is provided (file path or directory), run all 3 analyzers on that scope only.

For the full pipeline logic, follow the `bob:quality-orchestrator` skill — Pipeline 1 (Analysis).
