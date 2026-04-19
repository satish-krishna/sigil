# Bob

Autonomous code quality engine for Claude Code. Analyzes your codebase, prioritizes findings, fixes them with TDD, and creates PRs — all on a schedule.

## Skills

| Skill                  | Purpose                                                        |
| ---------------------- | -------------------------------------------------------------- |
| `tech-debt-analyzer`   | Find tech debt, SOLID violations, memory leaks, dead code      |
| `enhancement-finder`   | Find missing UX patterns, accessibility gaps, API improvements |
| `code-simplifier`      | Find overly complex code and propose simplifications           |
| `auto-fixer`           | Fix a single finding with TDD (test first, then fix, then PR)  |
| `test-gap-filler`      | Find untested code paths and write tests                       |
| `quality-orchestrator` | Scheduled entry point — chains analysis → prioritize → fix     |

## Commands

| Command                 | Description                                  |
| ----------------------- | -------------------------------------------- |
| `/bob:audit [scope]`    | Run all 3 analyzers, produce combined report |
| `/bob:fix [finding-id]` | Fix one finding with TDD + create PR         |
| `/bob:simplify [scope]` | Find and simplify complex code               |
| `/bob:enhance [scope]`  | Find enhancement opportunities               |

## Scheduled Operation

- **Monday 3 AM**: Full analysis (tech debt + enhancements + simplification)
- **Tue–Fri 4 AM**: Fix top 3 findings from backlog, create PRs

## Installation

```bash
# Add as a git submodule in your project's .claude/plugins/ directory
cd your-project
mkdir -p .claude/plugins
git submodule add https://github.com/satish-krishna/bob.git .claude/plugins/bob
```

Or clone directly:

```bash
git clone https://github.com/satish-krishna/bob.git .claude/plugins/bob
```

Then run `/bob:audit` to generate your first backlog.

## Stack Support

Auto-detects your tech stack and loads relevant reference files:

- **.NET** → `references/dotnet.md`
- **Angular** → `references/angular.md`
- Add `references/<stack>.md` for additional stacks

## How It Works

```
Monday 3 AM ─── 3 analyzers run in parallel ─── merged + prioritized backlog
                                                          │
Tue-Fri 4 AM ── pick top 3 findings ── for each:         │
                    │                                     │
                    ├─ validate finding still exists       │
                    ├─ create branch                      │
                    ├─ RED: write failing test             │
                    ├─ GREEN: minimal fix                  │
                    ├─ VERIFY: full test suite + build     │
                    └─ PR: commit, push, gh pr create     │
                                                          │
You wake up to PRs ◄─────────────────────────────────────┘
```

## State Files

Runtime state lives in `state/` (gitignored):

- `backlog.json` — prioritized queue of findings
- `history.json` — completed fixes with PR URLs
