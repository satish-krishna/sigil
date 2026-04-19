# Agent Mapping - Quick Reference

This file maps Claude Code commands to their responsible agents and explains when to use each agent directly.

## 📊 Command → Agent Mapping

| Command            | Agent                 | Purpose                             | Complexity  | Best For                                        |
| ------------------ | --------------------- | ----------------------------------- | ----------- | ----------------------------------------------- |
| `/forge:plan`      | **sirji** (Tech Lead) | Analyze complexity, define approach | Low-Medium  | All new tasks, deciding implementation strategy |
| `/forge:implement` | **chotu** (Engineer)  | Execute full-stack implementation   | Medium-High | Building features, fixing bugs                  |

---

## 🤖 Both Agents Explained

### 1. **chotu** - Fullstack Engineer

**Agent file:** `.claude/agents/chotu.md`

**Specializes in:**

- Angular 20 standalone components (OnPush, inject(), control flow)
- FastEndpoints with Result<T> error handling
- Entity Framework Core with PostgreSQL
- Jest + Spectator Testing Library for frontend tests
- xUnit + FakeItEasy for .NET testing
- Tailwind CSS styling with utility-first approach
- Build validation and optimization

**Used by commands:**

- ✅ `/forge:implement` - Main implementation executor

**Direct use (if needed):**

```
@chotu "implement the user authentication service with proper error handling"
@chotu "add Jest unit tests for the login component"
@chotu "debug the build failure in Angular frontend"
```

**Works best when:**

- You have a detailed plan from `/forge:plan`
- You need both frontend and backend changes
- You're adding tests to existing code
- You need quick prototyping with quality

---

### 2. **sirji** - Technical Lead Reviewer

**Agent file:** `.claude/agents/sirji.md`

**Specializes in:**

- Complexity assessment and tier classification
- Alternative approach analysis
- Architecture and design pattern validation
- Full-stack integration review
- SOLID principles application
- Code quality and standards enforcement
- Performance optimization recommendations
- Security pattern validation

**Used by commands:**

- ✅ `/forge:plan` - Creates implementation plans with complexity scoring
- ✅ `/forge:implement` - Reviews implementation automatically before commit

**Direct use (if needed):**

```
@sirji "analyze performance bottlenecks in the authentication flow"
@sirji "identify refactoring opportunities in the API client"
```

**Works best when:**

- You need architectural decisions
- You want a second opinion on approach
- You need complex feature planning
- You're optimizing existing code
- You need code quality assurance

---

## 🔄 Common Workflows & Agent Usage

### Scenario 1: Implementing a New Feature

```
1. Use /forge:plan
   └─ Agent: sirji (analyzes complexity, defines approach)

2. Use /forge:implement
   └─ Agent: chotu (implements frontend + backend)
   └─ Agent: sirji (reviews chotu's work automatically)

3. Done! Code committed when approved
```

### Scenario 2: Quick Bug Fix

```
1. Use /forge:plan (quick)
   └─ Agent: sirji (minimal planning)

2. Use /forge:implement
   └─ Agent: chotu (fixes the bug)
   └─ Agent: sirji (reviews automatically)

3. Done! Code committed
```

### Scenario 3: Complex Feature with Unknowns

```
1. Use /forge:plan
   └─ Agent: sirji (analyzes complexity, considers alternatives)

2. Use /forge:implement
   └─ Agent: chotu (implements per plan)
   └─ Agent: sirji (reviews automatically)

3. Done! Code committed
```

---

## 🎯 When to Use Agents Directly (Not Via Commands)

### Use **chotu** directly when:

- You need quick prototyping or exploration
- You're debugging build/test issues
- You need implementation outside the `/forge:implement` workflow
- You want specific code patterns

### Use **sirji** directly when:

- You need architectural advice
- You're analyzing performance issues
- You want to evaluate multiple approaches
- You need refactoring guidance

---

## 🚨 Agent Do's and Don'ts

### ✅ Do

- Let sirji plan before implementing
- Let chotu implement from plans
- Let sirji review before committing (automatic)
- Use agents for their specialties
- Give context and examples

### ❌ Don't

- Skip sirji's review (quality gate)
- Ignore chotu's test failures
- Skip planning for complex features
- Use agents without context
- Override quality gates

---

## 📞 Quick Decision Tree

```
"What do I need?"

├─ To analyze complexity?
│  └─ → Use @sirji or /forge:plan
│
├─ To implement code?
│  └─ → Use @chotu or /forge:implement
│
├─ To review code?
│  └─ → Automatic in /forge:implement (sirji)
│
└─ "I'm not sure"
   └─ → Use /forge:plan (sirji will analyze)
```

---

## 🔐 Agent Permissions

All agents work within safety constraints defined in `.claude/settings.local.json`:

- Limited bash execution (no deployment, no destructive commands)
- No access to external systems
- No secret management
- Required approval gates for commits
- Session tracking for audit trails

See `settings.local.json` for detailed permission model.

---

Last updated: 2026-01-08
