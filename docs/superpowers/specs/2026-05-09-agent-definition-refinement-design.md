# Agent Definition Refinement — Design Spec

**Date:** 2026-05-09
**Status:** Draft for review
**Affects:** `Sigil.Core` (records + interfaces), `.bob/docs/sigil-architecture-blueprint.md`, `README.md`
**Defers to follow-on issues:** `Sigil.Agent.SDK` runtime implementation, `Sigil.Runtime` planner update, `Sigil.Api` protocol endpoints

---

## 1. Concept

A Sigil **Agent** is a configured instance of a generic SDK runtime. It is composed of four pillars:

1. **The SDK** (`Sigil.Agent.SDK`) — the universal runtime. It owns the `/sigil/*` protocol surface, snapshot ingestion, system-prompt composition, model invocation via `IChatClient`, delta production, and lifecycle-hook dispatch.
2. **A model spec** — a structured choice of provider + model + sampling parameters. The agent constructs its own `IChatClient`; the kernel knows the spec for validation and telemetry tagging.
3. **A system prompt** — a base prompt plus dynamically composed skill bodies and an auto-rendered tool catalog. The SDK builds the effective prompt per execution step.
4. **Tools and skills** — the agent's verbs and packaged know-how. Tools are external (MCP servers, HTTP endpoints) or in-process C# delegates. Skills are Claude-style first-class behaviors that the kernel's planner can route to.

Agent authors describe **what** the agent is (a JSON manifest, skill markdown files, optional C# hooks and in-process tool delegates). The SDK provides **how** it runs.

Hosting model: **strict 1:1** — one container, one agent identity. Skill content lives inside the agent container; there is no kernel-curated skill catalog.

---

## 2. Data model

All types live in `Sigil.Core/Registry/` and `Sigil.Core/Storage/`. Records are immutable and use `init`-only setters in line with the rest of `Sigil.Core`.

### 2.1 `AgentRegistration`

```csharp
public sealed record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string EndpointUrl { get; init; }
    public string SemanticVersion { get; init; } = "1.0.0";
    public int RoutingWeight { get; init; } = 100;          // 0–100, canary/A-B
    public AgentStatus Status { get; init; } = AgentStatus.Starting;

    public required ModelSpec Model { get; init; }
    public IReadOnlyList<Skill> Skills { get; init; } = [];
    public IReadOnlyList<ToolBinding> Tools { get; init; } = [];
    public int? MaxTokenBudget { get; init; }

    public SecurityProfile Security { get; init; } = new();
    public AgentMetadata Metadata { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
}
```

### 2.2 `Skill`

The routable unit. The kernel's planner selects an `(agent, skill)` pair per execution step.

```csharp
public sealed record Skill
{
    public required string Name { get; init; }              // e.g. "summarize-pdf"
    public required string Description { get; init; }       // used by planner for selection
    public IReadOnlyList<string> RequiredTools { get; init; } = [];  // names match ToolBinding.Name
    public int? EstimatedMaxTokens { get; init; }
    public string Version { get; init; } = "1.0.0";
}
```

### 2.3 `ModelSpec`

```csharp
public sealed record ModelSpec
{
    public required string Provider { get; init; }   // "openai" | "azure-openai" | "anthropic" | "ollama" | ...
    public required string Model { get; init; }      // "gpt-4o-mini"
    public Sampling Sampling { get; init; } = new();
}

public sealed record Sampling
{
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
}
```

The kernel uses `ModelSpec` for tier-based policy ("this agent's tier may not use `gpt-4-turbo`") and for tagging traces. The agent constructs its own `IChatClient` from the spec — the kernel does not instantiate model clients.

### 2.4 `ToolBinding`

```csharp
public sealed record ToolBinding
{
    public required string Name { get; init; }              // e.g. "get_forecast"
    public required ToolKind Kind { get; init; }            // Mcp | Http | InProcess
    public required string Description { get; init; }
    public required string ParameterSchema { get; init; }   // raw JSON-schema text
}

public enum ToolKind { Mcp, Http, InProcess }
```

`ParameterSchema` is opaque JSON-schema text. Tools come from many sources (MCP servers, OpenAPI specs, hand-written) and all already speak JSON-schema; modeling a parser in `Sigil.Core` would be premature.

Connection details (HTTP base URL, auth tokens, MCP server addresses) stay agent-side. `ToolBinding` is what the kernel sees; secrets never cross the wire.

The manifest (§3.1) uses `parameterSchemaFile` as an ergonomic file reference; the SDK reads the file at boot and supplies its contents as `ToolBinding.ParameterSchema` in the emitted `AgentRegistration`.

### 2.5 `AgentMetadata`

```csharp
public sealed record AgentMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
```

Carries free-form tags only.

### 2.6 `SecurityProfile`

```csharp
public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];   // names from ToolBinding
}
```

`AllowedTools` keys by `ToolBinding.Name`. The runtime intersects `skill.RequiredTools ∩ Security.AllowedTools` to decide what the model sees per step.

### 2.7 `IAgentRegistrationStore`

```csharp
public interface IAgentRegistrationStore
{
    Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default);
    Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default);
    Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default);
}
```

### 2.8 Validation rules at registration time

The store validates:

- `Skill.Name` is non-empty and unique within the registration.
- `Skill.RequiredTools` resolves: every name appears in `Tools`.
- `ToolBinding.Name` is unique within the registration.
- `Security.AllowedTools` is a subset of `{ Tools[*].Name }`.

A registration that fails validation returns `Result.Failure(reason)` from `RegisterAsync`.

---

## 3. Authoring an agent

An agent container holds three artifacts:

### 3.1 `agent.json` — the static manifest

```json
{
  "agentId": "weather-bot",
  "name": "Weather Bot",
  "domain": "weather",
  "version": "1.0.0",
  "endpointUrl": "https://weather-bot.internal:8443",
  "routingWeight": 100,

  "model": {
    "provider": "openai",
    "model": "gpt-4o-mini",
    "sampling": { "temperature": 0.2, "maxOutputTokens": 800 }
  },

  "maxTokenBudget": 4000,
  "systemPromptFile": "prompts/system.md",
  "skillsDirectory": "skills/",

  "tools": [
    {
      "name": "get_forecast",
      "kind": "Http",
      "description": "Fetch a 7-day forecast for a given city.",
      "parameterSchemaFile": "tools/get_forecast.schema.json",
      "endpoint": "https://api.weather.example/v1/forecast",
      "auth": { "type": "bearer", "tokenEnv": "WEATHER_API_TOKEN" }
    },
    {
      "name": "search_news",
      "kind": "Mcp",
      "description": "Search news headlines via MCP.",
      "parameterSchemaFile": "tools/search_news.schema.json",
      "mcpServer": "https://mcp.example.com"
    }
  ],

  "security": { "isPiiCleared": false, "allowedTools": ["get_forecast", "search_news"] },
  "tags": { "team": "platform", "tier": "standard" }
}
```

The SDK loads and validates the manifest at boot, parses skill files from `skillsDirectory`, merges in any in-process tools registered programmatically, and emits an `AgentRegistration` to the kernel.

### 3.2 `skills/*.md` — Claude-style skill files

```markdown
---
name: forecast-summary
description: Summarize a multi-day weather forecast in 2–3 sentences. Use when the user wants a quick overview, not raw data.
requiredTools: [get_forecast]
estimatedMaxTokens: 400
version: 1.0.0
---

# Forecast Summary

When invoked:
1. Call `get_forecast` for the requested city.
2. Identify the dominant pattern (e.g. "rainy week", "warming trend").
3. Reply in 2–3 sentences. Lead with the headline; mention the highest and lowest temps.

Avoid restating raw daily data unless the user asks.
```

Frontmatter parses into the `Skill` record. The body loads into memory and composes into the system prompt at execution time (§4).

### 3.3 `Program.cs` — hooks and in-process tools

```csharp
var host = AgentHost.CreateBuilder(args)
    .UseManifest("agent.json")
    .RegisterInProcessTool("now_utc",
        description: "Returns current UTC time.",
        parameterSchema: "{ \"type\":\"object\",\"properties\":{} }",
        handler: _ => DateTime.UtcNow.ToString("O"))
    .OnSnapshotReceived(ctx => /* scrub or transform inbound snapshot */)
    .OnBeforeModelCall(ctx => /* mutate prompt or tool list */)
    .OnAfterModelCall(ctx => /* inspect raw model output */)
    .OnDeltaProduced(ctx => /* tag or filter outbound delta */)
    .Build();

await host.RunAsync();
```

In-process tools registered here merge with manifest-declared tools at boot; each emits a `ToolBinding(Kind = InProcess)`.

---

## 4. Runtime composition

When `/sigil/execute` arrives, the SDK runs:

1. Validate ETag, fetch step. The step references a skill by `SkillName`.
2. Hook: `OnSnapshotReceived(snapshot)` — may transform the snapshot.
3. Compose the system prompt:
   - Base prompt body from `systemPromptFile`.
   - Active skill body from `skills/{SkillName}.md`.
   - Auto-rendered tool catalog: name, description, parameter schema for each `ToolBinding` in `skill.RequiredTools ∩ Security.AllowedTools`.
4. Hook: `OnBeforeModelCall(messages, tools)` — may mutate either.
5. Invoke `IChatClient` with the composed messages and function-call tools.
6. Hook: `OnAfterModelCall(modelResponse)`.
7. Drive the tool-call loop (model → tool → model) until terminal.
8. Convert the terminal response into a `ContextDelta`.
9. Hook: `OnDeltaProduced(delta)` — may filter.
10. Return `AgentExecutionResult(delta, logs, metrics)`.

The model sees only tools the active skill needs **and** that the agent's `Security.AllowedTools` permits. This narrows attack surface and prompt size per step.

### 4.1 Lifecycle hook contract

```csharp
public interface IAgentLifecycleContext
{
    AgentExecutionPackage Package { get; }
    JobId JobId { get; }
    StepId StepId { get; }
    ILogger Logger { get; }
}

public delegate Task<ContextSnapshot> OnSnapshotReceived(IAgentLifecycleContext ctx, ContextSnapshot snapshot);
public delegate Task<(IList<ChatMessage>, IList<ToolBinding>)> OnBeforeModelCall(IAgentLifecycleContext ctx, IList<ChatMessage> messages, IList<ToolBinding> tools);
public delegate Task OnAfterModelCall(IAgentLifecycleContext ctx, ChatResponse response);
public delegate Task<ContextDelta> OnDeltaProduced(IAgentLifecycleContext ctx, ContextDelta delta);
```

A hook that throws aborts the step. The `AgentExecutionResult` carries the failed hook's name in its logs — no silent swallowing.

---

## 5. Protocol

The protocol records (`AgentExecutionPackage`, `AgentTask`, `ValidationResult`) land with the SDK runtime issue, not this one (see §8). They are documented here as the contract this design commits the SDK and `Sigil.Api` workstreams to. The vocabulary speaks skills and tools.

### 5.1 `/sigil/info` (GET)

Returns the full `AgentRegistration`. Refreshed by the kernel on heartbeat.

### 5.2 `/sigil/validate` (POST)

```csharp
public record AgentExecutionPackage
{
    public AgentTask Task { get; init; } = default!;
    public ContextSnapshot Snapshot { get; init; } = default!;
    public ETag ExpectedETag { get; init; } = default!;
}

public record AgentTask
{
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public required string SkillName { get; init; }
    public string Input { get; init; } = "";
    public IReadOnlyList<string> AvailableTools { get; init; } = [];
}

public record ValidationResult
{
    public bool CanHandle { get; init; }
    public int? EstimatedTokens { get; init; }
    public IReadOnlyList<string> MissingTools { get; init; } = [];
    public string? Reason { get; init; }
}
```

The agent's `/sigil/validate` checks:
- The skill named in `AgentTask.SkillName` exists.
- Every tool in that skill's `RequiredTools` resolves locally and is in `Security.AllowedTools`.
- The estimated token cost fits within `MaxTokenBudget`.
- The model is reachable.

### 5.3 `/sigil/execute` (POST)

Same `AgentExecutionPackage` in, `AgentExecutionResult` out. Internal flow per §4.

### 5.4 `/sigil/heartbeat` (POST)

Unchanged.

---

## 6. Planner expectations

Although planner code lives in `Sigil.Runtime` (out of scope for this spec to implement), this design constrains the contract:

- `IPlanner` candidate filtering keys off `AgentRegistration.Skills`, not capabilities.
- A `PlanStep` references a skill by `SkillName`.
- `LlmPlanner`'s prompt builder renders each agent's skills with name, description, required tools, and estimated max tokens. The LLM picks `(agent_id, skill_name)` per step.

These constraints are noted so the planner workstream lands consistent with this design.

---

## 7. File-level changes

### 7.1 `Sigil.Core`

| Action | Path |
|---|---|
| Add | `Registry/Skill.cs` |
| Add | `Registry/ModelSpec.cs` (with `Sampling`) |
| Add | `Registry/ToolBinding.cs` (with `ToolKind`) |
| Modify | `Registry/AgentRegistration.cs` |
| Modify | `Registry/AgentMetadata.cs` |
| Delete | `Registry/Capability.cs` |
| Modify | `Storage/IAgentRegistrationStore.cs` (`FindBySkillAsync`) |

### 7.2 `Sigil.Core.Tests`

| Action | Path |
|---|---|
| Add | `Registry/SkillTests.cs` — equality, JSON round-trip, validation rules |
| Add | `Registry/ModelSpecTests.cs` — equality, JSON round-trip, sampling defaults |
| Add | `Registry/ToolBindingTests.cs` — equality, JSON round-trip, kind discriminator |
| Add | `Registry/AgentRegistrationTests.cs` — round-trip with skills/tools/model populated |

### 7.3 Documentation

| Action | Path |
|---|---|
| Modify | `.bob/docs/sigil-architecture-blueprint.md` — §2 mapping table, §3 agent task vocabulary, §4.1 data model + store interface, §4.2 planner candidate filter and prompt template, new §4.x **Anatomy of an Agent** subsection covering manifest + skills + Program.cs + composition pipeline + lifecycle hooks. Also add a row to §10 noting kernel-curated skill catalog as a deferred question. |
| Modify | `README.md` — Stack bullet for the agent runtime, Phase 1 checklist row for `Sigil.Agent.SDK` |

All blueprint and code edits describe the present. They do not narrate the change from a prior shape.

---

## 8. Scope boundaries

**In scope for the issue this spec drives:**
- The `Sigil.Core` records and interface above.
- The `Sigil.Core.Tests` listed above.
- The blueprint and README edits.

**Out of scope (follow-on issues):**
- `Sigil.Agent.SDK` runtime — manifest loader, skill markdown parser, system-prompt composer, lifecycle-hook dispatcher, in-process tool registration, MCP/HTTP tool invocation.
- `Sigil.Api` protocol endpoints (`/sigil/*`).
- `Sigil.Runtime` planner update (`FindBySkillAsync` consumer, `PlanStep.SkillName`, `LlmPlanner` prompt template).
- Storage provider implementations (`Sigil.Storage.Mongo`, `Sigil.Storage.EfCore`) for `AgentRegistration` reads/writes.

---

## 9. Deferred questions

- **Kernel-curated skill catalog.** Today skills are agent-bundled. A future kernel-curated catalog (`ISkillStore`) would enable cross-agent reuse, central governance, and skill versioning across the fleet. Recorded for §10 of the blueprint.
- **Snapshot transformation surface.** `OnSnapshotReceived` lets an agent scrub or rewrite an inbound snapshot. The audit story for that mutation (does the kernel see the original or the transformed snapshot? does the delta have to be reconciled against the original?) is not yet pinned down. Will surface during SDK runtime implementation.
- **Tool-credential rotation.** `auth.tokenEnv` references an environment variable. A rotation flow (per-step short-lived credentials minted by the kernel and injected into the SDK) is implied by §4.4 of the blueprint but not specified here.
