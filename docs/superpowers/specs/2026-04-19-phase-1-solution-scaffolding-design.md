# Phase 1 · Solution Scaffolding — Design

**Issue:** [#1](https://github.com/satish-krishna/sigil/issues/1)
**Blueprint ref:** `.bob/docs/sigil-architecture-blueprint.md` §7.2
**Branch:** `chore/phase-1-solution-scaffolding`

## Goal

Stand up the initial .NET 9 solution with the seven core projects, build-level settings, and SDK pin so subsequent phase-1 issues have a compilable target.

## Scope

In:
- `sigil.sln` at repo root
- Seven projects under `src/` (see Projects table)
- `global.json` pinning SDK
- `Directory.Build.props` with shared compiler settings
- `dotnet build sigil.sln` succeeds with zero warnings

Out:
- Any domain code beyond what the compiler requires (no interfaces, no types)
- Sample agents under `src/agents/` (later issue)
- CI workflow (later issue)
- Docker / compose (later issue)

## Projects

| Project | Kind | References | Package refs |
|---|---|---|---|
| `Sigil.Core` | classlib | — | — |
| `Sigil.Agent.SDK` | classlib | Core | — |
| `Sigil.Storage.Mongo` | classlib | Core | `MongoDB.Driver` |
| `Sigil.Storage.EfCore` | classlib | Core | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` |
| `Sigil.Infrastructure` | classlib | Core | — |
| `Sigil.Runtime` | classlib | Core, Infrastructure | — |
| `Sigil.Api` | web | Runtime, Storage.Mongo, Storage.EfCore | `FastEndpoints`, `FastEndpoints.Swagger` |

## Build settings

**`global.json`**
```json
{ "sdk": { "version": "9.0.100", "rollForward": "latestFeature" } }
```

**`Directory.Build.props`** (repo root, applies to all projects)
- `TargetFramework` = `net9.0`
- `Nullable` = `enable`
- `ImplicitUsings` = `enable`
- `TreatWarningsAsErrors` = `true`
- `LangVersion` = `latest`

## API bootstrap

`Sigil.Api/Program.cs` — minimal FastEndpoints host; no endpoints yet.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFastEndpoints();
var app = builder.Build();
app.UseFastEndpoints();
app.Run();
```

## Verification

- `dotnet build sigil.sln` at repo root → success, zero warnings
- `Sigil.Core.csproj` contains no `<PackageReference>` or `<ProjectReference>` elements (architectural invariant per CLAUDE.md)

## Commit plan (conventional commits)

1. `chore: add global.json and Directory.Build.props`
2. `chore: scaffold Sigil.Core class library`
3. `chore: scaffold storage, infrastructure, runtime, SDK projects`
4. `chore: scaffold Sigil.Api with FastEndpoints`
5. `chore: add sigil.sln wiring all projects`
