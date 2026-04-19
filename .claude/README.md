# Claude Code Setup Guide - Siora

This guide helps engineers understand and use the Claude Code AI tooling setup for the Siora project.

## 🤖 What is Claude Code?

Claude Code is Anthropic's AI-powered development assistant that provides:

- **Intelligent Code Generation:** Context-aware fullstack implementation following project patterns
- **Automated Testing:** Comprehensive test generation with proper mocking strategies
- **Code Review:** Expert-level review following NextJS + .NET 8 standards
- **Workflow Automation:** Streamlined development processes with quality gates

## 🏗️ Our Claude Setup Architecture

### Project Structure

```text
.claude/                       # Claude Code configuration
├── README.md                  # This file - main setup guide
├── AGENT_MAPPING.md           # Command → Agent quick reference
├── settings.local.json        # Permissions and local settings
├── agents/                    # Specialized AI agents (2 agents)
│   ├── engineer.md            # chotu - Fullstack implementation specialist
│   └── tech-lead-reviewer.md  # sirji - Technical leadership & code review
└── commands/                  # Quick-access commands (forge namespace)
    ├── README.md              # Commands documentation
    └── forge/                 # Core implementation commands
        ├── plan.md            # /forge:plan - Task planning & complexity analysis
        └── implement.md       # /forge:implement - Full-stack implementation + automatic review
```

### Workflow Architecture

The workflow follows a simple **plan → implement → commit** cycle:

1. **Plan** (`/forge:plan`) - Analyze complexity, define approach (agent: sirji)
2. **Implement** (`/forge:implement`) - Execute implementation with automatic review gates (agent: chotu + sirji review)
3. **Commit** - Quality-assured code automatically committed when approved

This is a focused, lean workflow designed for Phase 3 development of the 001-user-auth feature branch.

## 🚀 Getting Started

### 1. Prerequisites

- Claude Code CLI installed and authenticated
- Access to this repository
- Understanding of the Siora architecture (see CLAUDE.md)

### 2. Available Commands

All commands use the `/forge:` prefix for organization.

#### **`/forge:plan [TASK_DESCRIPTION]`**

- **Agent:** sirji (Technical Lead)
- **Purpose:** Analyze complexity and create implementation plan
- **Output:** `.forge/plans/` directory with detailed approach
- **When to use:** Before starting any implementation task

```bash
/forge:plan "Add user authentication endpoint with session management"
```

#### **`/forge:implement [PLAN_FILE]`**

- **Agent:** chotu (Fullstack Engineer)
- **Purpose:** Execute implementation following the plan
- **Requires:** Plan file from `/forge:plan`
- **Output:** Implemented code + session files with audit trail
- **Quality Gates:** Builds, tests, linting, sirji review

```bash
/forge:implement plans/feature/user-auth-plan.md
```

### 3. Available Agents (Direct Use)

For specialized work outside the command workflow:

| Agent     | Specialty                                       | Usage                                          |
| --------- | ----------------------------------------------- | ---------------------------------------------- |
| **chotu** | Fullstack engineering, implementation           | `@chotu implement user authentication service` |
| **sirji** | Technical leadership, architecture, code review | `@sirji analyze API design for scalability`    |

See `AGENT_MAPPING.md` for detailed command → agent mapping.

## 💡 Typical Development Workflow

### Feature Implementation (Start to Finish)

```bash
# 1. Create a plan (analyzes complexity, defines approach)
/forge:plan "Implement OAuth sign-in with Google provider"

# 2. Implement following the plan (includes automatic review gate)
/forge:implement plans/feature/oauth-signin-plan.md

# 3. sirji reviews automatically and approves/requests changes
# If changes needed, address them and iterate

# Result: Approved implementation is automatically committed
```

### Quick Bug Fix (Low Complexity)

```bash
# Plan (usually trivial for bugs)
/forge:plan "Fix null reference error in login form validation"

# Implement
/forge:implement plans/bug/login-validation-plan.md

# Result: Bug fix committed when approved
```

## 📋 Siora-Specific Context

### What All Agents Know

- **Frontend:** Next.js 14+, React 19, TypeScript, Tailwind CSS, WebAwesome Pro, NextAuth
- **Backend:** ASP.NET Core 8, FastEndpoints, Entity Framework Core
- **Database:** PostgreSQL (Supabase) with Row Level Security
- **UI Framework:** Tailwind CSS for styling, WebAwesome Pro for components
- **Testing:** Jest + React Testing Library (frontend), xUnit + FakeItEasy (backend)
- **Build System:** npm (frontend), dotnet (backend)
- **Authentication:** Supabase Auth (OAuth), NextAuth (frontend)
- **Real-time:** SignalR for live messaging
- **AI:** Semantic Kernel, Google Gemini Flash
- **Code Patterns:** Established in CLAUDE.md

### Coding Standards (Automatically Enforced)

- Next.js functional components with React hooks
- TypeScript strict mode across frontend
- FastEndpoints with Result<T> error handling
- Tailwind CSS with semantic utility composition
- > 80% test coverage for new code

## 🎯 Common Scenarios

### Implementing a New Feature

→ Use `/forge:plan` → `/forge:implement`

### Fixing a Bug

→ Use `/forge:plan` (quick) → `/forge:implement`

### Complex Feature with Multiple Approaches

→ Use `/forge:plan` first (sirji analyzes alternatives and recommends simplest approach)

## 🔐 Security & Quality

### What Agents DON'T Do

- Deploy to any environment
- Modify external services or databases
- Skip quality gates or tests
- Commit secrets or sensitive data
- Create breaking changes without explicit instruction

### Quality Gates (Automatic)

- ✅ Builds pass (`npm run build` + `dotnet build`)
- ✅ Tests pass (>80% coverage)
- ✅ Linting passes (ESLint + StyleCop)
- ✅ Code review approved by sirji
- ✅ Session files track all changes
- ✅ No secrets in code

### Configuration

Modify `.claude/settings.local.json` to adjust:

- Permission scopes for agents
- Build command timeouts
- Test execution settings

## 📖 Additional Documentation

- **`AGENT_MAPPING.md`** - Quick reference for command → agent mapping
- **`commands/README.md`** - Detailed command documentation
- **`../CLAUDE.md`** - Comprehensive AI-specific project guidelines
- **`../CONTRIBUTING.md`** - Development workflow and standards

## 🎓 Learning Path

1. **Start here:** This file (README.md) - overview of how things work
2. **Understand agents:** Read `AGENT_MAPPING.md` - who does what
3. **First task:** Use `/forge:plan` then `/forge:implement` for a simple feature
4. **Deep dive:** Read `../CLAUDE.md` for comprehensive patterns and guidelines

## ❓ Troubleshooting

| Issue                                    | Solution                                                          |
| ---------------------------------------- | ----------------------------------------------------------------- |
| Agent ignores project patterns           | Check they read `CLAUDE.md` at start - point to specific examples |
| Build/test failures after implementation | Agents can debug and fix - use `/forge:improve-tests` or iterate  |
| Complexity assessment seems wrong        | Use `/forge:plan` to recalculate with sirji                       |
| Review feedback ignored                  | Iterate with `/forge:implement` - feedback loop continues         |
| Not sure what command to use             | Check `AGENT_MAPPING.md` for quick reference                      |

## 🔄 Quick Command Reference

```bash
# Planning & Complexity Analysis
/forge:plan "task description"

# Implementation with automatic review gate
/forge:implement plans/path/to/plan.md
```

Remember: All implementation commands include automatic quality gates. chotu implements, sirji reviews, and nothing commits without approval. This isn't negotiable—it's how we maintain standards.
