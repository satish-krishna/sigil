---
allowed-tools: Bash(gh issue view:*), Bash(gh search:*), Bash(gh issue list:*), Bash(gh pr comment:*), Bash(gh pr diff:*), Bash(gh pr view:*), Bash(gh pr list:*)
description: Code review a pull request against Siora standards and CLAUDE.md
disable-model-invocation: false
---

Provide a code review for the given pull request in the Siora repository.

To do this, follow these steps precisely:

1. Use a Haiku agent to check if the pull request (a) is closed, (b) is a draft, (c) does not need a code review (eg. because it is an automated pull request, or is very simple and obviously ok), or (d) already has a code review from you from earlier. If so, do not proceed.
2. Use another Haiku agent to give you a list of file paths to (but not the contents of) any relevant CLAUDE.md files from the codebase: the root CLAUDE.md file (if one exists), as well as any CLAUDE.md files in the directories whose files the pull request modified
3. Use a Haiku agent to view the pull request, and ask the agent to return a summary of the change
4. Then, launch 5 parallel Sonnet agents to independently code review the change. The agents should do the following, then return a list of issues and the reason each issue was flagged:
   a. Agent #1: Audit the changes to make sure they comply with the CLAUDE.md (Siora standards: no RxJS, Signals only, Standalone components, OnPush, FastEndpoints, etc.)
   b. Agent #2: Read the file changes in the pull request, then do a shallow scan for obvious bugs. Focus on large bugs, avoid nitpicks. Ignore likely false positives.
   c. Agent #3: Read the git blame and history of the code modified, to identify any bugs in light of that historical context
   d. Agent #4: Read previous pull requests that touched these files, and check for any comments on those pull requests that may also apply to the current pull request.
   e. Agent #5: Read code comments in the modified files, and make sure the changes comply with any guidance in the comments. Also check for Siora-specific anti-patterns: RxJS imports, NgModule, incorrect Spartan-NG usage, inline styles instead of Tailwind.
5. For each issue found in #4, launch a parallel Haiku agent that scores the issue 0-100 confidence:
   a. 0: False positive
   b. 25: Might be real, couldn't verify
   c. 50: Real issue but minor nitpick
   d. 75: Verified real issue, important
   e. 100: Absolutely certain, critical issue
6. Filter out any issues with a score less than 80. If there are no issues that meet this criteria, do not proceed.
7. Use a Haiku agent to repeat the eligibility check from #1.
8. Finally, use the gh bash command to comment back on the pull request with the result. When writing your comment:
   a. Keep output brief
   b. No emojis
   c. Link and cite relevant code, files, and URLs with full SHA

Siora-specific false positives to ignore:

- CSS custom properties used instead of Tailwind (sometimes intentional for theming)
- `inject()` usage (this is the correct modern DI pattern for Siora)
- Absence of constructor DI (inject() is preferred)
- Direct signal access without async pipe (correct for Siora)

Notes:

- Do not check build signal or attempt to build or typecheck the app.
- Use `gh` to interact with Github
- Make a todo list first
- You must cite and link each bug with full SHA

Final comment format:

---

### Code review

Found N issues:

1. <brief description> (CLAUDE.md says "<...>")

<link to file and line with full sha1 + line range>

2. <brief description> (some/CLAUDE.md says "<...>")

<link>

🤖 Generated with [Claude Code](https://claude.ai/code)

<sub>- If this code review was useful, please react with 👍. Otherwise, react with 👎.</sub>

---

Or if no issues:

---

### Code review

No issues found. Checked for bugs, Siora coding standards, and CLAUDE.md compliance.

🤖 Generated with [Claude Code](https://claude.ai/code)
