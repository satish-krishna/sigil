# Issue #6 — EF Core provider + initial migration (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land `Sigil.Storage.EfCore` — a PostgreSQL-backed `ISigilStore` (and `IAuditStore`) with atomic ETag-conditional commits, JSON-column persistence for the agent registration aggregates, and an initial migration. This is the **default v1 store**; `Sigil.Storage.Mongo` (#5) is the alternative.

**Architecture:** A single `SigilDbContext` owns five entity sets (`AgentRegistrations`, `Jobs`, `ContextStates`, `Checkpoints`, `AuditEntries`). Five sub-store classes each take the context and implement one Core abstraction; `EfSigilStore` aggregates them. Optimistic concurrency on `IContextStore.CommitDeltaAsync` is enforced by an explicit `ETag` text column updated via EF's `Where(...).ExecuteUpdateAsync(...)` — a single SQL `UPDATE ... WHERE etag = @expected` whose row-count is the conflict signal. Complex aggregates (`Skills`, `Tools`, `Security.AllowedTools`, `Metadata.Tags`, `ContextSnapshot.State`, `ContextDelta.Updates`, the `AgentLogEntry` log) persist as `jsonb` columns via value converters; `Skills[*].Name` is queryable through the JSON containment operator. `AuditEntries` is append-only — entity configuration removes the entity from `ChangeTracker` after insert and the type has no update or delete code path. DI surface is a flat `IServiceCollection.AddSigilEfCore(...)` extension matching the existing `AddSigilSecurity` / `AddAgentGateway` shape.

**Tech Stack:** .NET 9, EF Core 9.0.x with `Npgsql.EntityFrameworkCore.PostgreSQL`, `CSharpFunctionalExtensions.Result`, xUnit + Shouldly + `Testcontainers.PostgreSql` for integration tests. Central package management via `Directory.Packages.props`. `TreatWarningsAsErrors=true` is on globally.

**Issue:** <https://github.com/satish-krishna/sigil/issues/6>
**Blueprint:** `.bob/docs/sigil-architecture-blueprint.md` §4.5 (Atomic Context Bus), §4.8 (Storage Abstraction)
**Spec:** `docs/superpowers/specs/2026-05-09-agent-definition-refinement-design.md` §2 (data model, registration validation rules, `IAgentRegistrationStore`)

---

## Pre-flight notes (read before starting)

- **Working directory:** `D:\Repos\sigil`. All paths are relative to repo root.
- **Branch:** create `feat/efcore-provider` from `main` before Task 1.
- **Build/test commands:** `dotnet build sigil.sln` and `dotnet test sigil.sln`. Nullability warnings, unused usings, and analyzer warnings fail the build (`TreatWarningsAsErrors=true` globally).
- **Test framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`) with **Shouldly**. Do **not** use FluentAssertions — this repo standardizes on Shouldly (memory entry: `feedback_test_assertion_library.md`).
- **`Result<T>` type:** `CSharpFunctionalExtensions.Result<T>`. Construct via `Result.Success(value)` / `Result.Failure<T>("error-code")`. Inspect via `result.IsSuccess`, `result.IsFailure`, `result.Value`, `result.Error`.
- **`Maybe<T>` type:** `CSharpFunctionalExtensions.Maybe<T>`. Empty via `Maybe<T>.None`; populated via `Maybe.From(value)`.
- **`AgentRegistration`** is a sealed record with custom `Equals`/`GetHashCode` (`SequenceEqual` over `Skills` and `Tools`). Round-trip equality through EF must preserve list order — preserve that in the JSON value-converter design.
- **`ETag`/`AgentId`/`JobId`/`StepId`** are `readonly record struct`s wrapping `string Value`. EF column mapping uses `HasConversion(v => v.Value, s => new T(s))`.
- **`SigilBuilder` does not exist yet.** Issue #6's deliverable text says "`UseEfCore(...)` extension on `SigilBuilder`" but the established pattern in this repo is flat `IServiceCollection.AddXxx(...)` extensions (`AddSigilSecurity`, `AddAgentGateway`). This plan ships `IServiceCollection.AddSigilEfCore(...)` for parity. A future `SigilBuilder` (introduced in #5 or a dedicated refactor) can wrap this.
- **Docker required for integration tests.** Testcontainers spins up Postgres in a real container. CI must have Docker available; locally, `docker info` should succeed before `dotnet test` runs the EF Core integration tests.
- **Hooks:** `PostToolUse` runs Prettier on edited `.cs` files. If a Prettier reformat changes whitespace after a Write/Edit, accept it and proceed.
- **No before/after framing** in code comments, blueprint edits, or commit prose (memory: `feedback_no_historical_framing.md`). Documentation describes the present, not the journey.
- **Commit cadence:** one commit per task. Conventional-commit prefixes: `feat(storage)`, `chore(build)`, `test(storage)`, `docs(plans)`, `docs(blueprint)`. Match recent log style: `feat(infra): Sigil-Key validation, Open tier (closes #4) (#26)`.

### Design decisions locked here

| Decision | Choice | Rationale |
|---|---|---|
| Default DB engine | **PostgreSQL** | Per the user's stated v1 default. Matches the blueprint's §4.8 example (`UseNpgsql`). |
| Optimistic-concurrency mechanism | **Explicit `ETag` text column updated via `ExecuteUpdateAsync` with a `Where(... etag == expected)` filter** | Honors the `ETag` value type already in `Sigil.Core`. Atomic single-statement compare-and-set; row-count is the conflict signal. Portable across EF providers (works in SQLite for unit tests). Avoids the Postgres-only `xmin` column and the EF `[ConcurrencyToken]` exception model (we want a `Result.Failure`, not a thrown `DbUpdateConcurrencyException`). |
| Aggregate persistence (`Skills`, `Tools`, `Security.AllowedTools`, `Metadata.Tags`, `ContextSnapshot.State`, `ContextDelta.Updates`, log entries) | **`jsonb` columns + value converters** | Round-trip integrity is the primary requirement — these are immutable records. Owned-type tables would explode dictionary tags and string lists into multi-table joins for no relational query benefit. `Skills[*].Name` queryability is preserved via Postgres `jsonb_path_exists` / `@>` containment, indexed by GIN. |
| `EfAgentRegistrationStore.FindBySkillAsync` query shape | **`EF.Functions.JsonContains(Skills, ...)` over the `jsonb` column**, with a GIN index on `Skills` | Postgres-native and fast. Falls back to a LINQ-side filter if the provider rejects the operator (defensive). |
| Sub-store DI lifetime | **Scoped** (matches `AddDbContext` default) | EF `DbContext` is not thread-safe; scoped binding keeps one context per request/operation. |
| Job ↔ context-state coupling | **`EfJobStore.CreateAsync` writes the `Job` row and a paired empty `ContextStateRecord` in the same `SaveChangesAsync`** | A `Job` always has exactly one context state. Co-creation eliminates race conditions on first `GetSnapshotAsync` and removes the need for an `INSERT-IF-NOT-EXISTS` shape on read. |
| Audit-store immutability | **Configuration-level: no update/delete methods on `EfAuditStore`; entity has no concurrency token; tests assert that `LogChangeAsync` followed by `LogChangeAsync` produces two distinct rows even when content is identical** | Aligns with the CLAUDE.md "Immutable audit" principle. The contract has no Update/Delete; nothing to enforce at runtime beyond not exposing one. |
| Migration design-time bootstrap | **`SigilDbContextFactory : IDesignTimeDbContextFactory<SigilDbContext>`** in `Sigil.Storage.EfCore/Internal/` | Lets `dotnet ef migrations add` work without a startup project that has wired DI. Reads the connection string from an env var with a localhost fallback. |
| `AddSigilEfCore` shape | **`(IServiceCollection, IConfiguration)`** matching `AddSigilSecurity` / `AddAgentGateway`. Internally calls `AddDbContext<SigilDbContext>(o => o.UseNpgsql(opts.ConnectionString))` plus scoped registrations for the five sub-stores and `ISigilStore`. | Repo-consistent. The blueprint's `sigil.UseEfCore(o => o.UseNpgsql(...))` shape becomes a follow-on once `SigilBuilder` lands. |
| Storage-layer error codes | **`Sigil.Storage.EfCore/StorageErrors.cs`** with `etag-mismatch`, `not-found`, `validation-*`, `duplicate-agent` | Mirrors `SigilSecurityErrors` / `SigilGatewayErrors` style — flat string constants the orchestrator can switch on. **Not** in `Sigil.Core` because they're storage-flavored; if Mongo (#5) needs the same codes, hoist them in a follow-up. |

### File structure summary

| File | Responsibility | Created in |
|---|---|---|
| `Directory.Packages.props` | Add `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Relational`, `Testcontainers.PostgreSql`. EF Core packages are already present. | Task 1 |
| `src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj` | Reference Npgsql provider package | Task 2 |
| `src/Sigil.Storage.EfCore/SigilEfCoreOptions.cs` | `IOptions`-bound config (`ConnectionString`, optional `MigrateOnStartup`) | Task 3 |
| `src/Sigil.Storage.EfCore/StorageErrors.cs` | Stable string error codes | Task 4 |
| `src/Sigil.Storage.EfCore/Internal/JsonValueConverters.cs` | `IReadOnlyList<T>` / `IReadOnlyDictionary<string,object>` ↔ `jsonb` text converters + value comparers | Task 5 |
| `src/Sigil.Storage.EfCore/Persistence/ContextStateRecord.cs` | Internal entity for one job's context (JobId, ETag, State, Log) | Task 6 |
| `src/Sigil.Storage.EfCore/SigilDbContext.cs` | `DbContext` with five `DbSet<>` properties; applies configurations | Tasks 7, 9–13 |
| `src/Sigil.Storage.EfCore/Configuration/AgentRegistrationConfig.cs` | `IEntityTypeConfiguration<AgentRegistration>` — strongly-typed-id conversions, `jsonb` collections, GIN index on `Skills` | Task 9 |
| `src/Sigil.Storage.EfCore/Configuration/JobConfig.cs` | `Job` config | Task 10 |
| `src/Sigil.Storage.EfCore/Configuration/ContextStateConfig.cs` | `ContextStateRecord` config — ETag column, `jsonb` State/Log | Task 11 |
| `src/Sigil.Storage.EfCore/Configuration/CheckpointConfig.cs` | `Checkpoint` config | Task 12 |
| `src/Sigil.Storage.EfCore/Configuration/AuditEntryConfig.cs` | `AuditEntry` config (no concurrency token; explicit no-update test) | Task 13 |
| `src/Sigil.Storage.EfCore/Internal/SigilDbContextFactory.cs` | `IDesignTimeDbContextFactory<SigilDbContext>` for `dotnet ef` | Task 14 |
| `src/Sigil.Storage.EfCore/Migrations/<timestamp>_Initial.cs` (+ Designer + ModelSnapshot) | Generated initial migration | Task 15 |
| `src/Sigil.Storage.EfCore/EfAgentRegistrationStore.cs` | `IAgentRegistrationStore` impl with §2.8 validation rules | Task 16 |
| `src/Sigil.Storage.EfCore/EfJobStore.cs` | `IJobStore` impl + paired `ContextStateRecord` seed | Task 17 |
| `src/Sigil.Storage.EfCore/EfContextStore.cs` | `IContextStore` impl with `ExecuteUpdateAsync` ETag-conditional commit | Task 18 |
| `src/Sigil.Storage.EfCore/EfCheckpointStore.cs` | `ICheckpointStore` impl | Task 19 |
| `src/Sigil.Storage.EfCore/EfAuditStore.cs` | `IAuditStore` impl (append-only) | Task 20 |
| `src/Sigil.Storage.EfCore/EfSigilStore.cs` | Aggregator wiring the five sub-stores | Task 21 |
| `src/Sigil.Storage.EfCore/ServiceCollectionExtensions.cs` | `AddSigilEfCore` DI extension | Task 22 |
| `tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj` | New test project | Task 7 |
| `tests/Sigil.Storage.EfCore.Tests/Infrastructure/PostgresFixture.cs` | xUnit `IAsyncLifetime` collection fixture launching a Testcontainers Postgres + applying migrations | Task 8 |
| `tests/Sigil.Storage.EfCore.Tests/Infrastructure/SigilDbCollection.cs` | xUnit `[CollectionDefinition]` so tests share the container | Task 8 |
| `tests/Sigil.Storage.EfCore.Tests/JsonValueConvertersTests.cs` | Pure unit tests for the converters | Task 5 |
| `tests/Sigil.Storage.EfCore.Tests/Configuration/*Tests.cs` | Per-config schema-shape tests using `ctx.Model.FindEntityType(...)` | Tasks 9–13 |
| `tests/Sigil.Storage.EfCore.Tests/EfAgentRegistrationStoreTests.cs` | CRUD, FindBySkill, FindByDomain, validation (§2.8), heartbeat, status | Task 16 |
| `tests/Sigil.Storage.EfCore.Tests/EfJobStoreTests.cs` | Create/Get/UpdateStatus + asserts paired context-state row exists | Task 17 |
| `tests/Sigil.Storage.EfCore.Tests/EfContextStoreTests.cs` | **The key concurrency tests:** parallel commits, ETag mismatch, log append/read | Task 18 |
| `tests/Sigil.Storage.EfCore.Tests/EfCheckpointStoreTests.cs` | CRUD + Resolve + GetPendingForJob | Task 19 |
| `tests/Sigil.Storage.EfCore.Tests/EfAuditStoreTests.cs` | Append-only behavior (two identical entries → two rows), GetHistory, GetAgentHistory | Task 20 |
| `tests/Sigil.Storage.EfCore.Tests/EfSigilStoreTests.cs` | Smoke test that `ISigilStore` resolves and exposes all five sub-stores | Task 21 |
| `tests/Sigil.Storage.EfCore.Tests/AddSigilEfCoreTests.cs` | DI smoke + options binding | Task 22 |
| `Roadmap.md` | Flip "v1 default / used by Docker Compose" annotation from #5 to #6 | Task 23 |
| `.bob/docs/sigil-architecture-blueprint.md` | §9 phase plan: list EF Core before Mongo. §4.8 consumer-registration code: present EF Core as the default option. | Task 23 |
| `README.md` | Quickstart now references EF Core/Postgres | Task 23 |
| `sigil.sln` | Add the new test project | Task 7 |

---

## Task 1: Add Postgres + Testcontainers package versions to CPM

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Pick the package versions**

  - `Npgsql.EntityFrameworkCore.PostgreSQL`: highest stable `9.0.x` aligned with EF Core 9.0.15 already pinned in CPM. As of writing this plan, `9.0.4` is the latest 9.0 line.
  - `Microsoft.EntityFrameworkCore.Relational`: 9.0.15 (matches the existing `Microsoft.EntityFrameworkCore` pin — needed transitively for `IEntityTypeConfiguration` and migration tooling).
  - `Testcontainers.PostgreSql`: highest stable in the 4.x line (e.g., `4.0.0`+). The `Testcontainers` family ships in lockstep so resolve to one version that satisfies both `Testcontainers.PostgreSql` and its transitive `Testcontainers` dependency. If `dotnet restore` flags `NU1605`/`NU1109` under `CentralPackageTransitivePinningEnabled=true`, raise the conflicting transitive package in CPM rather than downgrading.

- [ ] **Step 2: Edit `Directory.Packages.props`**

  Insert these `<PackageVersion>` entries inside the existing main `<ItemGroup>` (the block currently containing `Microsoft.EntityFrameworkCore` etc.):

  ```xml
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.15" />
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
  ```

  Append a new `<ItemGroup>` for test-only Testcontainers below the existing test-only block:

  ```xml
  <!-- Test-only — Testcontainers -->
  <ItemGroup>
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.0.0" />
  </ItemGroup>
  ```

  Replace the version literals if Step 1 selected different ones; record the chosen versions in the Task 1 commit message.

- [ ] **Step 3: Verify `dotnet restore sigil.sln` succeeds**

  Run: `dotnet restore sigil.sln`
  Expected: clean restore, no `NU1605`/`NU1109`. No project consumes the new packages yet, so this is a no-op for the build graph.

- [ ] **Step 4: Verify the build is still green**

  Run: `dotnet build sigil.sln`
  Expected: clean build with no warnings.

- [ ] **Step 5: Commit**

  ```bash
  git checkout -b feat/efcore-provider
  git add Directory.Packages.props
  git commit -m "chore(build): add Npgsql + Testcontainers package versions to CPM"
  ```

---

## Task 2: Wire the Npgsql provider into `Sigil.Storage.EfCore.csproj`

**Files:**
- Modify: `src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`

- [ ] **Step 1: Add the package reference**

  Under the existing `<ItemGroup>` that lists `Microsoft.EntityFrameworkCore`, add:

  ```xml
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  ```

  The full `<ItemGroup>` should now read:

  ```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  ```

- [ ] **Step 2: Verify the build is still green**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build. No code consumes the package yet; this just confirms restore + transitive pinning resolved.

- [ ] **Step 3: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj
  git commit -m "chore(storage): reference Npgsql EF Core provider"
  ```

---

## Task 3: `SigilEfCoreOptions` — bound configuration

**Files:**
- Create: `src/Sigil.Storage.EfCore/SigilEfCoreOptions.cs`

- [ ] **Step 1: Write the type**

  ```csharp
  namespace Sigil.Storage.EfCore;

  public sealed class SigilEfCoreOptions
  {
      public const string SectionName = "Storage:EfCore";

      public string ConnectionString { get; set; } = "";

      // When true, AddSigilEfCore applies pending migrations on startup.
      // Off by default — production deployments should run migrations as a
      // separate step.
      public bool MigrateOnStartup { get; set; }
  }
  ```

- [ ] **Step 2: Verify the build is still green**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build.

- [ ] **Step 3: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/SigilEfCoreOptions.cs
  git commit -m "feat(storage): add SigilEfCoreOptions"
  ```

---

## Task 4: `StorageErrors` — error-code constants

**Files:**
- Create: `src/Sigil.Storage.EfCore/StorageErrors.cs`

- [ ] **Step 1: Write the type**

  ```csharp
  namespace Sigil.Storage.EfCore;

  public static class StorageErrors
  {
      public const string EtagMismatch       = "etag-mismatch";
      public const string NotFound           = "not-found";
      public const string DuplicateAgent     = "duplicate-agent";
      public const string ValidationSkillName        = "validation/skill-name-empty";
      public const string ValidationSkillDuplicate   = "validation/skill-duplicate";
      public const string ValidationSkillRequiresUnknownTool = "validation/skill-requires-unknown-tool";
      public const string ValidationToolNameDuplicate = "validation/tool-duplicate";
      public const string ValidationAllowedToolUnknown = "validation/allowed-tool-unknown";
  }
  ```

- [ ] **Step 2: Verify the build is still green**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build.

- [ ] **Step 3: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/StorageErrors.cs
  git commit -m "feat(storage): add StorageErrors codes"
  ```

---

## Task 5: JSON value converters and comparers

The `AgentRegistration` aggregate (and `ContextSnapshot.State`, `ContextDelta.Updates`, `AgentLogEntry` log) needs to round-trip through `jsonb` columns. Without explicit value comparers, EF cannot detect changes inside reference types and `SequenceEqual`-based record equality breaks. This task ships the converters and tests them in isolation.

**Files:**
- Create: `src/Sigil.Storage.EfCore/Internal/JsonValueConverters.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj` (also done in Task 7 — defer csproj writes until Task 7 and put the test file there)
- Create (after Task 7): `tests/Sigil.Storage.EfCore.Tests/JsonValueConvertersTests.cs`

- [ ] **Step 1: Write the converter source**

  ```csharp
  using System.Collections.ObjectModel;
  using System.Text.Json;
  using Microsoft.EntityFrameworkCore.ChangeTracking;
  using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

  namespace Sigil.Storage.EfCore.Internal;

  internal static class JsonValueConverters
  {
      private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

      public static ValueConverter<IReadOnlyList<T>, string> ReadOnlyListConverter<T>() =>
          new(
              v => JsonSerializer.Serialize(v, Json),
              s => JsonSerializer.Deserialize<List<T>>(s, Json) ?? new List<T>());

      public static ValueComparer<IReadOnlyList<T>> ReadOnlyListComparer<T>() =>
          new(
              (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
              v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
              v => v.ToList());

      public static ValueConverter<IReadOnlyDictionary<string, string>, string> StringMapConverter() =>
          new(
              v => JsonSerializer.Serialize(v, Json),
              s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json) ?? new());

      public static ValueComparer<IReadOnlyDictionary<string, string>> StringMapComparer() =>
          new(
              (a, b) => DictEquals(a, b, StringComparer.Ordinal),
              v => HashDict(v),
              v => new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(v)));

      public static ValueConverter<IReadOnlyDictionary<string, object>, string> ObjectMapConverter() =>
          new(
              v => JsonSerializer.Serialize(v, Json),
              s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, Json) ?? new());

      public static ValueComparer<IReadOnlyDictionary<string, object>> ObjectMapComparer() =>
          new(
              (a, b) => DictEquals(a, b, EqualityComparer<object>.Default),
              v => HashDict(v),
              v => new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(v)));

      public static ValueConverter<Dictionary<string, object>, string> MutableObjectMapConverter() =>
          new(
              v => JsonSerializer.Serialize(v, Json),
              s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, Json) ?? new());

      public static ValueComparer<Dictionary<string, object>> MutableObjectMapComparer() =>
          new(
              (a, b) => DictEquals(a, b, EqualityComparer<object>.Default),
              v => HashDict(v),
              v => new Dictionary<string, object>(v));

      public static ValueConverter<string[], string> StringArrayConverter() =>
          new(
              v => JsonSerializer.Serialize(v, Json),
              s => JsonSerializer.Deserialize<string[]>(s, Json) ?? Array.Empty<string>());

      public static ValueComparer<string[]> StringArrayComparer() =>
          new(
              (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
              v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
              v => v.ToArray());

      private static bool DictEquals<TKey, TVal>(
          IReadOnlyDictionary<TKey, TVal>? a,
          IReadOnlyDictionary<TKey, TVal>? b,
          IEqualityComparer<TVal> valueCmp)
      {
          if (a is null && b is null) return true;
          if (a is null || b is null) return false;
          if (a.Count != b.Count) return false;
          foreach (var kvp in a)
          {
              if (!b.TryGetValue(kvp.Key, out var bv)) return false;
              if (!valueCmp.Equals(kvp.Value, bv)) return false;
          }
          return true;
      }

      private static int HashDict<TKey, TVal>(IReadOnlyDictionary<TKey, TVal> v)
          where TKey : notnull
      {
          var hash = new HashCode();
          foreach (var kvp in v.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal))
          {
              hash.Add(kvp.Key);
              hash.Add(kvp.Value);
          }
          return hash.ToHashCode();
      }
  }
  ```

- [ ] **Step 2: Verify it compiles**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build.

- [ ] **Step 3: Commit (tests for this file land in Task 7 once the test project exists)**

  ```bash
  git add src/Sigil.Storage.EfCore/Internal/JsonValueConverters.cs
  git commit -m "feat(storage): add JSON value converters for jsonb columns"
  ```

---

## Task 6: `ContextStateRecord` — internal entity for `IContextStore`

`IContextStore` exposes `ContextSnapshot` (a record) but the persisted shape needs an explicit `ETag` column for the conditional update. We model that as an internal entity.

**Files:**
- Create: `src/Sigil.Storage.EfCore/Persistence/ContextStateRecord.cs`

- [ ] **Step 1: Write the entity**

  ```csharp
  using Sigil.Core.Protocol;

  namespace Sigil.Storage.EfCore.Persistence;

  internal sealed class ContextStateRecord
  {
      // PK — string form of JobId.Value
      public string JobId { get; set; } = "";

      // Compare-and-set token. Updated atomically with State.
      public string ETag { get; set; } = "";

      // Snapshot state. Stored as jsonb.
      public IReadOnlyDictionary<string, object> State { get; set; }
          = new Dictionary<string, object>();

      // Append-only log of AgentLogEntry. Stored as jsonb.
      public IReadOnlyList<AgentLogEntry> Log { get; set; } = new List<AgentLogEntry>();
  }
  ```

- [ ] **Step 2: Verify it compiles**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build.

- [ ] **Step 3: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Persistence/ContextStateRecord.cs
  git commit -m "feat(storage): add ContextStateRecord internal entity"
  ```

---

## Task 7: Test project bootstrap + first JSON-converter test

Create `tests/Sigil.Storage.EfCore.Tests/`, register it in the solution, write the JSON-converter unit tests created in Task 5.

**Files:**
- Create: `tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj`
- Create: `tests/Sigil.Storage.EfCore.Tests/JsonValueConvertersTests.cs`
- Modify: `sigil.sln`

- [ ] **Step 1: Create the test project file**

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <IsPackable>false</IsPackable>
      <IsTestProject>true</IsTestProject>
      <!-- Tests touch internals of Sigil.Storage.EfCore (e.g. JsonValueConverters). -->
      <NoWarn>$(NoWarn);CS1591</NoWarn>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.NET.Test.Sdk" />
      <PackageReference Include="xunit" />
      <PackageReference Include="xunit.runner.visualstudio">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      </PackageReference>
      <PackageReference Include="Shouldly" />
      <PackageReference Include="Microsoft.EntityFrameworkCore" />
      <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
      <PackageReference Include="Microsoft.Extensions.Configuration" />
      <PackageReference Include="Microsoft.Extensions.Configuration.Binder" />
      <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
      <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
      <PackageReference Include="Microsoft.Extensions.Options" />
      <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
      <PackageReference Include="Testcontainers.PostgreSql" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\..\src\Sigil.Storage.EfCore\Sigil.Storage.EfCore.csproj" />
    </ItemGroup>
  </Project>
  ```

- [ ] **Step 2: Expose internals to the test project**

  Add to `src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj` inside an `<ItemGroup>`:

  ```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Sigil.Storage.EfCore.Tests" />
  </ItemGroup>
  ```

- [ ] **Step 3: Add the test project to the solution**

  Run:

  ```bash
  dotnet sln sigil.sln add tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj
  ```

- [ ] **Step 4: Write the failing converter tests**

  Create `tests/Sigil.Storage.EfCore.Tests/JsonValueConvertersTests.cs`:

  ```csharp
  using Shouldly;
  using Sigil.Storage.EfCore.Internal;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  public class JsonValueConvertersTests
  {
      [Fact]
      public void ReadOnlyList_RoundTrips_Preserving_Order()
      {
          var conv = JsonValueConverters.ReadOnlyListConverter<string>();
          IReadOnlyList<string> input = new[] { "a", "b", "c" };
          var serialized = conv.ConvertToProvider(input)!.ToString();
          var roundTripped = (IReadOnlyList<string>)conv.ConvertFromProvider(serialized)!;
          roundTripped.ShouldBe(input);
      }

      [Fact]
      public void StringArray_RoundTrips()
      {
          var conv = JsonValueConverters.StringArrayConverter();
          var input = new[] { "x", "y" };
          var serialized = conv.ConvertToProvider(input)!.ToString();
          var roundTripped = (string[])conv.ConvertFromProvider(serialized)!;
          roundTripped.ShouldBe(input);
      }

      [Fact]
      public void StringMap_RoundTrips()
      {
          var conv = JsonValueConverters.StringMapConverter();
          IReadOnlyDictionary<string, string> input = new Dictionary<string, string>
          {
              ["team"] = "platform",
              ["tier"] = "open"
          };
          var serialized = conv.ConvertToProvider(input)!.ToString();
          var roundTripped = (IReadOnlyDictionary<string, string>)conv.ConvertFromProvider(serialized)!;
          roundTripped["team"].ShouldBe("platform");
          roundTripped["tier"].ShouldBe("open");
          roundTripped.Count.ShouldBe(2);
      }

      [Fact]
      public void ReadOnlyListComparer_DetectsContentDifference()
      {
          var cmp = JsonValueConverters.ReadOnlyListComparer<string>();
          IReadOnlyList<string> a = new[] { "a", "b" };
          IReadOnlyList<string> b = new[] { "a", "c" };
          cmp.Equals(a, b).ShouldBeFalse();
      }

      [Fact]
      public void StringMapComparer_DetectsKeyAdded()
      {
          var cmp = JsonValueConverters.StringMapComparer();
          IReadOnlyDictionary<string, string> a = new Dictionary<string, string> { ["k"] = "v" };
          IReadOnlyDictionary<string, string> b = new Dictionary<string, string>
          {
              ["k"] = "v",
              ["k2"] = "v2"
          };
          cmp.Equals(a, b).ShouldBeFalse();
      }

      [Fact]
      public void StringMapComparer_OrderInsensitiveEquality()
      {
          var cmp = JsonValueConverters.StringMapComparer();
          IReadOnlyDictionary<string, string> a = new Dictionary<string, string>
          {
              ["a"] = "1",
              ["b"] = "2"
          };
          IReadOnlyDictionary<string, string> b = new Dictionary<string, string>
          {
              ["b"] = "2",
              ["a"] = "1"
          };
          cmp.Equals(a, b).ShouldBeTrue();
      }
  }
  ```

- [ ] **Step 5: Run the converter tests**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj`
  Expected: all six tests pass.

- [ ] **Step 6: Commit**

  ```bash
  git add tests/Sigil.Storage.EfCore.Tests src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj sigil.sln
  git commit -m "test(storage): scaffold EfCore test project + converter tests"
  ```

---

## Task 8: `PostgresFixture` — Testcontainers collection fixture

A single Postgres container, started once per test run, with migrations applied. Tests share the database via `[Collection("SigilDb")]` and use random schema/db names per test method to avoid cross-test pollution.

**Files:**
- Create: `tests/Sigil.Storage.EfCore.Tests/Infrastructure/PostgresFixture.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Infrastructure/SigilDbCollection.cs`

- [ ] **Step 1: Write the fixture**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Sigil.Storage.EfCore;
  using Testcontainers.PostgreSql;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Infrastructure;

  public sealed class PostgresFixture : IAsyncLifetime
  {
      private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
          .WithImage("postgres:16-alpine")
          .WithDatabase("sigil_test")
          .WithUsername("sigil")
          .WithPassword("sigil")
          .Build();

      public string ConnectionString => _container.GetConnectionString();

      public SigilDbContext NewContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql(ConnectionString)
              .Options;
          return new SigilDbContext(opts);
      }

      public async Task InitializeAsync()
      {
          await _container.StartAsync();
          await using var ctx = NewContext();
          await ctx.Database.MigrateAsync();
      }

      public Task DisposeAsync() => _container.DisposeAsync().AsTask();
  }
  ```

- [ ] **Step 2: Write the collection definition**

  ```csharp
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Infrastructure;

  [CollectionDefinition("SigilDb")]
  public sealed class SigilDbCollection : ICollectionFixture<PostgresFixture> { }
  ```

- [ ] **Step 3: Verify it compiles (the fixture will fail at runtime until `SigilDbContext` exists in Task 9 and migrations land in Task 15 — that's expected)**

  Run: `dotnet build tests/Sigil.Storage.EfCore.Tests/Sigil.Storage.EfCore.Tests.csproj`
  Expected: this build will fail with `SigilDbContext` not found. Continue to Task 9 — the fixture compiles end-to-end after Task 9.

- [ ] **Step 4: Commit (compilation will pass once Task 9 lands; fixture is referenced but not instantiated by any test yet)**

  ```bash
  git add tests/Sigil.Storage.EfCore.Tests/Infrastructure
  git commit -m "test(storage): add Testcontainers Postgres fixture (compiles after Task 9)" --allow-empty
  ```

  Note: do not stage if it leaves the build broken. If the build is broken, stash this and revisit after Task 9 — squash into Task 9's commit.

---

## Task 9: `SigilDbContext` shell + `AgentRegistration` configuration

`AgentRegistration` is the most complex aggregate. We tackle it first because the shape it sets (jsonb collections + GIN index + strongly-typed-id conversions) becomes the template for the others. The DbContext shell is created here so tests can compile.

**Files:**
- Create: `src/Sigil.Storage.EfCore/SigilDbContext.cs`
- Create: `src/Sigil.Storage.EfCore/Configuration/AgentRegistrationConfig.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Configuration/AgentRegistrationConfigTests.cs`

- [ ] **Step 1: Write the failing entity-config test**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Core.Registry;
  using Sigil.Storage.EfCore;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Configuration;

  public class AgentRegistrationConfigTests
  {
      private static SigilDbContext NewModelOnlyContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql("Host=unused")
              .Options;
          return new SigilDbContext(opts);
      }

      [Fact]
      public void AgentRegistration_KeyIsAgentIdString()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
          var pk = et.FindPrimaryKey()!;
          pk.Properties.ShouldHaveSingleItem();
          pk.Properties[0].Name.ShouldBe(nameof(AgentRegistration.AgentId));
      }

      [Fact]
      public void Skills_IsJsonbColumn()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
          var skillsProp = et.FindProperty(nameof(AgentRegistration.Skills))!;
          skillsProp.GetColumnType().ShouldBe("jsonb");
      }

      [Fact]
      public void Tools_IsJsonbColumn()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
          var toolsProp = et.FindProperty(nameof(AgentRegistration.Tools))!;
          toolsProp.GetColumnType().ShouldBe("jsonb");
      }

      [Fact]
      public void Skills_HasGinIndex()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
          et.GetIndexes().ShouldContain(
              i => i.Properties.Count == 1
                && i.Properties[0].Name == nameof(AgentRegistration.Skills),
              customMessage: "Expected a GIN index on Skills for jsonb containment queries");
      }
  }
  ```

- [ ] **Step 2: Run the test to verify it fails (compile error or `FindEntityType` returns null)**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AgentRegistrationConfigTests"`
  Expected: FAIL — `SigilDbContext` does not exist or the entity isn't configured.

- [ ] **Step 3: Write `SigilDbContext` shell**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Audit;
  using Sigil.Core.Checkpoints;
  using Sigil.Core.Jobs;
  using Sigil.Core.Registry;
  using Sigil.Storage.EfCore.Persistence;

  namespace Sigil.Storage.EfCore;

  public sealed class SigilDbContext : DbContext
  {
      public SigilDbContext(DbContextOptions<SigilDbContext> options) : base(options) { }

      public DbSet<AgentRegistration>      AgentRegistrations  => Set<AgentRegistration>();
      public DbSet<Job>                    Jobs                => Set<Job>();
      internal DbSet<ContextStateRecord>   ContextStates       => Set<ContextStateRecord>();
      public DbSet<Checkpoint>             Checkpoints         => Set<Checkpoint>();
      public DbSet<AuditEntry>             AuditEntries        => Set<AuditEntry>();

      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          modelBuilder.ApplyConfigurationsFromAssembly(typeof(SigilDbContext).Assembly);
      }
  }
  ```

- [ ] **Step 4: Write `AgentRegistrationConfig`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Sigil.Core.Identity;
  using Sigil.Core.Registry;
  using Sigil.Storage.EfCore.Internal;

  namespace Sigil.Storage.EfCore.Configuration;

  internal sealed class AgentRegistrationConfig : IEntityTypeConfiguration<AgentRegistration>
  {
      public void Configure(EntityTypeBuilder<AgentRegistration> e)
      {
          e.ToTable("agent_registrations");

          // PK — strongly-typed AgentId persisted as text.
          e.HasKey(x => x.AgentId);
          e.Property(x => x.AgentId)
              .HasConversion(v => v.Value, s => new AgentId(s))
              .HasColumnName("agent_id")
              .HasColumnType("text")
              .ValueGeneratedNever();

          e.Property(x => x.Name).HasColumnName("name").HasColumnType("text").IsRequired();
          e.Property(x => x.Domain).HasColumnName("domain").HasColumnType("text").IsRequired();
          e.Property(x => x.EndpointUrl).HasColumnName("endpoint_url").HasColumnType("text").IsRequired();
          e.Property(x => x.SemanticVersion).HasColumnName("semantic_version").HasColumnType("text").IsRequired();
          e.Property(x => x.RoutingWeight).HasColumnName("routing_weight").IsRequired();
          e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("text").IsRequired();
          e.Property(x => x.MaxTokenBudget).HasColumnName("max_token_budget");
          e.Property(x => x.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamptz").IsRequired();
          e.Property(x => x.LastHeartbeat).HasColumnName("last_heartbeat").HasColumnType("timestamptz").IsRequired();

          // ModelSpec (owned, single row inline)
          e.OwnsOne(x => x.Model, m =>
          {
              m.Property(p => p.Provider).HasColumnName("model_provider").HasColumnType("text").IsRequired();
              m.Property(p => p.Model).HasColumnName("model_name").HasColumnType("text").IsRequired();
              m.OwnsOne(p => p.Sampling, s =>
              {
                  s.Property(p => p.Temperature).HasColumnName("model_temperature");
                  s.Property(p => p.TopP).HasColumnName("model_top_p");
                  s.Property(p => p.MaxOutputTokens).HasColumnName("model_max_output_tokens");
              });
          });
          e.Navigation(x => x.Model).IsRequired();

          // Skills + Tools — jsonb with value converters.
          var skillsProp = e.Property(x => x.Skills)
              .HasColumnName("skills")
              .HasColumnType("jsonb")
              .IsRequired();
          skillsProp.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<Skill>());
          skillsProp.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<Skill>());

          var toolsProp = e.Property(x => x.Tools)
              .HasColumnName("tools")
              .HasColumnType("jsonb")
              .IsRequired();
          toolsProp.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<ToolBinding>());
          toolsProp.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<ToolBinding>());

          // SecurityProfile (owned, with jsonb AllowedTools).
          e.OwnsOne(x => x.Security, s =>
          {
              s.Property(p => p.CertificateThumbprint).HasColumnName("security_cert_thumbprint");
              s.Property(p => p.SigilKey).HasColumnName("security_sigil_key");
              s.Property(p => p.IsPiiCleared).HasColumnName("security_is_pii_cleared").IsRequired();
              s.Property(p => p.Tier)
                  .HasColumnName("security_tier")
                  .HasConversion<string>()
                  .HasColumnType("text")
                  .IsRequired();

              var allowed = s.Property(p => p.AllowedTools)
                  .HasColumnName("security_allowed_tools")
                  .HasColumnType("jsonb")
                  .IsRequired();
              allowed.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<string>());
              allowed.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<string>());
          });

          // Metadata (owned, with jsonb Tags).
          e.OwnsOne(x => x.Metadata, m =>
          {
              var tags = m.Property(p => p.Tags)
                  .HasColumnName("metadata_tags")
                  .HasColumnType("jsonb")
                  .IsRequired();
              tags.Metadata.SetValueConverter(JsonValueConverters.StringMapConverter());
              tags.Metadata.SetValueComparer(JsonValueConverters.StringMapComparer());
          });

          // GIN index for FindBySkillAsync — Postgres jsonb containment.
          e.HasIndex(x => x.Skills)
              .HasMethod("gin")
              .HasDatabaseName("ix_agent_registrations_skills_gin");

          e.HasIndex(x => x.Domain).HasDatabaseName("ix_agent_registrations_domain");
      }
  }
  ```

- [ ] **Step 5: Run the AgentRegistration config tests to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AgentRegistrationConfigTests"`
  Expected: all four tests pass.

- [ ] **Step 6: Verify the whole solution builds**

  Run: `dotnet build sigil.sln`
  Expected: clean build (the `PostgresFixture` from Task 8 now compiles).

- [ ] **Step 7: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/SigilDbContext.cs src/Sigil.Storage.EfCore/Configuration/AgentRegistrationConfig.cs tests/Sigil.Storage.EfCore.Tests/Configuration
  git commit -m "feat(storage): SigilDbContext + AgentRegistration entity configuration"
  ```

---

## Task 10: `JobConfig`

**Files:**
- Create: `src/Sigil.Storage.EfCore/Configuration/JobConfig.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Configuration/JobConfigTests.cs`

- [ ] **Step 1: Write the failing test**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Core.Jobs;
  using Sigil.Storage.EfCore;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Configuration;

  public class JobConfigTests
  {
      private static SigilDbContext NewModelOnlyContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql("Host=unused")
              .Options;
          return new SigilDbContext(opts);
      }

      [Fact]
      public void Job_KeyIsJobIdString()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(Job))!;
          var pk = et.FindPrimaryKey()!;
          pk.Properties[0].Name.ShouldBe(nameof(Job.JobId));
      }

      [Fact]
      public void Status_IsString()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(Job))!;
          et.FindProperty(nameof(Job.Status))!.GetColumnType().ShouldBe("text");
      }
  }
  ```

- [ ] **Step 2: Run the test to verify it fails**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~JobConfigTests"`
  Expected: FAIL — `Job` is not in the model yet.

- [ ] **Step 3: Write `JobConfig`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Sigil.Core.Identity;
  using Sigil.Core.Jobs;

  namespace Sigil.Storage.EfCore.Configuration;

  internal sealed class JobConfig : IEntityTypeConfiguration<Job>
  {
      public void Configure(EntityTypeBuilder<Job> e)
      {
          e.ToTable("jobs");

          e.HasKey(x => x.JobId);
          e.Property(x => x.JobId)
              .HasConversion(v => v.Value, s => new JobId(s))
              .HasColumnName("job_id")
              .HasColumnType("text")
              .ValueGeneratedNever();

          e.Property(x => x.Status)
              .HasConversion<string>()
              .HasColumnName("status")
              .HasColumnType("text")
              .IsRequired();

          e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
          e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
      }
  }
  ```

- [ ] **Step 4: Run the test to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~JobConfigTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Configuration/JobConfig.cs tests/Sigil.Storage.EfCore.Tests/Configuration/JobConfigTests.cs
  git commit -m "feat(storage): Job entity configuration"
  ```

---

## Task 11: `ContextStateConfig`

The single most important configuration: declares the `ETag` text column that Task 18's `ExecuteUpdateAsync` filters on, plus jsonb columns for `State` and `Log`. This config has **no** `[ConcurrencyCheck]` — we want our own `Result.Failure(StorageErrors.EtagMismatch)` based on row-count, not EF's `DbUpdateConcurrencyException`.

**Files:**
- Create: `src/Sigil.Storage.EfCore/Configuration/ContextStateConfig.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Configuration/ContextStateConfigTests.cs`

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Persistence;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Configuration;

  public class ContextStateConfigTests
  {
      private static SigilDbContext NewModelOnlyContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql("Host=unused")
              .Options;
          return new SigilDbContext(opts);
      }

      [Fact]
      public void ContextState_KeyIsJobId()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
          et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(ContextStateRecord.JobId));
      }

      [Fact]
      public void ETag_IsTextColumn_NoConcurrencyToken()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
          var etag = et.FindProperty(nameof(ContextStateRecord.ETag))!;
          etag.GetColumnType().ShouldBe("text");
          etag.IsConcurrencyToken.ShouldBeFalse(
              "ETag-mismatch must surface as Result.Failure via ExecuteUpdate row-count, not DbUpdateConcurrencyException");
      }

      [Fact]
      public void State_IsJsonbColumn()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
          et.FindProperty(nameof(ContextStateRecord.State))!.GetColumnType().ShouldBe("jsonb");
      }

      [Fact]
      public void Log_IsJsonbColumn()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
          et.FindProperty(nameof(ContextStateRecord.Log))!.GetColumnType().ShouldBe("jsonb");
      }
  }
  ```

- [ ] **Step 2: Run to verify they fail**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~ContextStateConfigTests"`
  Expected: FAIL — entity not yet configured.

- [ ] **Step 3: Write `ContextStateConfig`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Sigil.Core.Protocol;
  using Sigil.Storage.EfCore.Internal;
  using Sigil.Storage.EfCore.Persistence;

  namespace Sigil.Storage.EfCore.Configuration;

  internal sealed class ContextStateConfig : IEntityTypeConfiguration<ContextStateRecord>
  {
      public void Configure(EntityTypeBuilder<ContextStateRecord> e)
      {
          e.ToTable("context_states");

          e.HasKey(x => x.JobId);
          e.Property(x => x.JobId)
              .HasColumnName("job_id")
              .HasColumnType("text")
              .ValueGeneratedNever();

          e.Property(x => x.ETag)
              .HasColumnName("etag")
              .HasColumnType("text")
              .IsRequired();

          var state = e.Property(x => x.State)
              .HasColumnName("state")
              .HasColumnType("jsonb")
              .IsRequired();
          state.Metadata.SetValueConverter(JsonValueConverters.ObjectMapConverter());
          state.Metadata.SetValueComparer(JsonValueConverters.ObjectMapComparer());

          var log = e.Property(x => x.Log)
              .HasColumnName("log")
              .HasColumnType("jsonb")
              .IsRequired();
          log.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<AgentLogEntry>());
          log.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<AgentLogEntry>());
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~ContextStateConfigTests"`
  Expected: all four tests pass.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Configuration/ContextStateConfig.cs tests/Sigil.Storage.EfCore.Tests/Configuration/ContextStateConfigTests.cs
  git commit -m "feat(storage): ContextStateRecord entity configuration"
  ```

---

## Task 12: `CheckpointConfig`

**Files:**
- Create: `src/Sigil.Storage.EfCore/Configuration/CheckpointConfig.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Configuration/CheckpointConfigTests.cs`

- [ ] **Step 1: Write the failing test**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Core.Checkpoints;
  using Sigil.Storage.EfCore;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Configuration;

  public class CheckpointConfigTests
  {
      private static SigilDbContext NewModelOnlyContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql("Host=unused")
              .Options;
          return new SigilDbContext(opts);
      }

      [Fact]
      public void Checkpoint_KeyIsCheckpointId()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(Checkpoint))!;
          et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(Checkpoint.CheckpointId));
      }

      [Fact]
      public void JobId_IsIndexed()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(Checkpoint))!;
          et.GetIndexes().ShouldContain(i =>
              i.Properties.Count == 1 && i.Properties[0].Name == nameof(Checkpoint.JobId));
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~CheckpointConfigTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `CheckpointConfig`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Sigil.Core.Checkpoints;
  using Sigil.Core.Identity;

  namespace Sigil.Storage.EfCore.Configuration;

  internal sealed class CheckpointConfig : IEntityTypeConfiguration<Checkpoint>
  {
      public void Configure(EntityTypeBuilder<Checkpoint> e)
      {
          e.ToTable("checkpoints");

          e.HasKey(x => x.CheckpointId);
          e.Property(x => x.CheckpointId).HasColumnName("checkpoint_id").HasColumnType("text").ValueGeneratedNever();

          e.Property(x => x.JobId)
              .HasConversion(v => v.Value, s => new JobId(s))
              .HasColumnName("job_id")
              .HasColumnType("text")
              .IsRequired();

          e.Property(x => x.StepId)
              .HasConversion(v => v.Value, s => new StepId(s))
              .HasColumnName("step_id")
              .HasColumnType("text")
              .IsRequired();

          e.Property(x => x.Status)
              .HasConversion<string>()
              .HasColumnName("status")
              .HasColumnType("text")
              .IsRequired();

          e.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
          e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
          e.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");

          e.HasIndex(x => x.JobId).HasDatabaseName("ix_checkpoints_job_id");
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~CheckpointConfigTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Configuration/CheckpointConfig.cs tests/Sigil.Storage.EfCore.Tests/Configuration/CheckpointConfigTests.cs
  git commit -m "feat(storage): Checkpoint entity configuration"
  ```

---

## Task 13: `AuditEntryConfig`

**Files:**
- Create: `src/Sigil.Storage.EfCore/Configuration/AuditEntryConfig.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/Configuration/AuditEntryConfigTests.cs`

- [ ] **Step 1: Write the failing test**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Core.Audit;
  using Sigil.Storage.EfCore;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests.Configuration;

  public class AuditEntryConfigTests
  {
      private static SigilDbContext NewModelOnlyContext()
      {
          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql("Host=unused")
              .Options;
          return new SigilDbContext(opts);
      }

      [Fact]
      public void AuditEntry_KeyIsAuditId()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
          et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(AuditEntry.AuditId));
      }

      [Fact]
      public void JobId_AgentId_AreIndexed()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
          var indexes = et.GetIndexes().ToList();
          indexes.ShouldContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AuditEntry.JobId));
          indexes.ShouldContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AuditEntry.AgentId));
      }

      [Fact]
      public void Delta_IsJsonbColumn()
      {
          using var ctx = NewModelOnlyContext();
          var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
          // Delta is owned + jsonb-mapped via Updates/Removals subproperties.
          var delta = et.FindNavigation(nameof(AuditEntry.Delta));
          delta.ShouldNotBeNull();
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AuditEntryConfigTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `AuditEntryConfig`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Sigil.Core.Audit;
  using Sigil.Core.Identity;
  using Sigil.Storage.EfCore.Internal;

  namespace Sigil.Storage.EfCore.Configuration;

  internal sealed class AuditEntryConfig : IEntityTypeConfiguration<AuditEntry>
  {
      public void Configure(EntityTypeBuilder<AuditEntry> e)
      {
          e.ToTable("audit_entries");

          e.HasKey(x => x.AuditId);
          e.Property(x => x.AuditId).HasColumnName("audit_id").HasColumnType("text").ValueGeneratedNever();

          e.Property(x => x.JobId)
              .HasConversion(v => v.Value, s => new JobId(s))
              .HasColumnName("job_id").HasColumnType("text").IsRequired();

          e.Property(x => x.AgentId)
              .HasConversion(v => v.Value, s => new AgentId(s))
              .HasColumnName("agent_id").HasColumnType("text").IsRequired();

          e.Property(x => x.StepId)
              .HasConversion(v => v.Value, s => new StepId(s))
              .HasColumnName("step_id").HasColumnType("text").IsRequired();

          e.Property(x => x.Timestamp).HasColumnName("timestamp").HasColumnType("timestamptz").IsRequired();

          // Delta — owned, with jsonb collections.
          e.OwnsOne(x => x.Delta, d =>
          {
              var updates = d.Property(p => p.Updates)
                  .HasColumnName("delta_updates")
                  .HasColumnType("jsonb")
                  .IsRequired();
              updates.Metadata.SetValueConverter(JsonValueConverters.MutableObjectMapConverter());
              updates.Metadata.SetValueComparer(JsonValueConverters.MutableObjectMapComparer());

              var removals = d.Property(p => p.Removals)
                  .HasColumnName("delta_removals")
                  .HasColumnType("jsonb")
                  .IsRequired();
              removals.Metadata.SetValueConverter(JsonValueConverters.StringArrayConverter());
              removals.Metadata.SetValueComparer(JsonValueConverters.StringArrayComparer());
          });

          // UsageMetrics — owned (model whatever fields the UsageMetrics record exposes).
          e.OwnsOne(x => x.Metrics, m =>
          {
              // Map every public property on UsageMetrics. If UsageMetrics gains fields
              // later, extend this configuration in the same task that adds them.
              m.ToTable("audit_entries"); // Inline columns
          });

          e.HasIndex(x => x.JobId).HasDatabaseName("ix_audit_entries_job_id");
          e.HasIndex(x => x.AgentId).HasDatabaseName("ix_audit_entries_agent_id");
          e.HasIndex(x => x.Timestamp).HasDatabaseName("ix_audit_entries_timestamp");
      }
  }
  ```

  > **Implementation note:** Read `src/Sigil.Core/Protocol/UsageMetrics.cs` before writing the `OwnsOne(x => x.Metrics, ...)` block. Map each public property explicitly with `HasColumnName("metrics_...")` so the migration generates a stable column set. If `UsageMetrics` is empty/marker-record-shaped, `OwnsOne` with no inner config is enough (EF skips empty owned types).

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AuditEntryConfigTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Configuration/AuditEntryConfig.cs tests/Sigil.Storage.EfCore.Tests/Configuration/AuditEntryConfigTests.cs
  git commit -m "feat(storage): AuditEntry entity configuration"
  ```

---

## Task 14: Design-time `SigilDbContextFactory`

`dotnet ef migrations add` needs a way to instantiate `SigilDbContext` at design time without running the API's DI graph.

**Files:**
- Create: `src/Sigil.Storage.EfCore/Internal/SigilDbContextFactory.cs`

- [ ] **Step 1: Write the factory**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Design;

  namespace Sigil.Storage.EfCore.Internal;

  internal sealed class SigilDbContextFactory : IDesignTimeDbContextFactory<SigilDbContext>
  {
      public SigilDbContext CreateDbContext(string[] args)
      {
          var connectionString = Environment.GetEnvironmentVariable("SIGIL_EFCORE_CONNECTION")
              ?? "Host=localhost;Port=5432;Database=sigil_design;Username=sigil;Password=sigil";

          var opts = new DbContextOptionsBuilder<SigilDbContext>()
              .UseNpgsql(connectionString)
              .Options;

          return new SigilDbContext(opts);
      }
  }
  ```

- [ ] **Step 2: Verify build**

  Run: `dotnet build src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj`
  Expected: clean build.

- [ ] **Step 3: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Internal/SigilDbContextFactory.cs
  git commit -m "feat(storage): design-time DbContext factory for ef tooling"
  ```

---

## Task 15: Initial migration

**Files:**
- Create: `src/Sigil.Storage.EfCore/Migrations/<timestamp>_Initial.cs` (+ `.Designer.cs`, + `SigilDbContextModelSnapshot.cs`)

- [ ] **Step 1: Generate the migration**

  Run:

  ```bash
  dotnet ef migrations add Initial --project src/Sigil.Storage.EfCore --startup-project src/Sigil.Storage.EfCore --output-dir Migrations
  ```

  Expected: three new files under `src/Sigil.Storage.EfCore/Migrations/`:
  - `<timestamp>_Initial.cs`
  - `<timestamp>_Initial.Designer.cs`
  - `SigilDbContextModelSnapshot.cs`

- [ ] **Step 2: Inspect the migration**

  Open the generated `Initial.cs`. Verify:
  - `agent_registrations` table created with all columns from Task 9 (jsonb for `skills`, `tools`, `security_allowed_tools`, `metadata_tags`).
  - `jobs`, `context_states`, `checkpoints`, `audit_entries` tables present.
  - GIN index on `agent_registrations.skills` (look for `migrationBuilder.Sql("CREATE INDEX ... USING gin (skills)")` or the EF Core 9 fluent equivalent that emits `USING gin`).
  - All `timestamptz` columns map to `timestamp with time zone`.

  If the GIN index isn't emitted (some EF Core versions ignore `HasMethod("gin")` on jsonb-converted properties), edit the generated migration to add a raw SQL statement at the bottom of `Up`:

  ```csharp
  migrationBuilder.Sql(
      "CREATE INDEX IF NOT EXISTS ix_agent_registrations_skills_gin " +
      "ON agent_registrations USING gin (skills);");
  ```

  Mirror it in `Down`:

  ```csharp
  migrationBuilder.Sql("DROP INDEX IF EXISTS ix_agent_registrations_skills_gin;");
  ```

- [ ] **Step 3: Verify the solution still builds**

  Run: `dotnet build sigil.sln`
  Expected: clean build.

- [ ] **Step 4: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/Migrations
  git commit -m "feat(storage): initial migration for EF Core provider"
  ```

---

## Task 16: `EfAgentRegistrationStore` + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfAgentRegistrationStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfAgentRegistrationStoreTests.cs`

This is the first sub-store backed by a real Postgres container. Validation rules from spec §2.8 are enforced in `RegisterAsync`.

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using CSharpFunctionalExtensions;
  using Shouldly;
  using Sigil.Core.Identity;
  using Sigil.Core.Registry;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfAgentRegistrationStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfAgentRegistrationStoreTests(PostgresFixture pg) => _pg = pg;

      private static AgentRegistration Sample(string id = "echo-agent", params string[] skillNames) =>
          new()
          {
              AgentId = new AgentId(id),
              Name = id,
              Domain = "test",
              EndpointUrl = "https://localhost",
              Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" },
              Skills = skillNames.Length == 0
                  ? new[] { new Skill { Name = "echo", Description = "echo" } }
                  : skillNames.Select(n => new Skill { Name = n, Description = n }).ToArray(),
              Tools = Array.Empty<ToolBinding>(),
          };

      [Fact]
      public async Task RegisterAsync_PersistsAndGetReturnsEqualValue()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          var reg = Sample("a-1", "skill-a");

          var registerResult = await store.RegisterAsync(reg);
          registerResult.IsSuccess.ShouldBeTrue();

          var fetched = await store.GetAsync(new AgentId("a-1"));
          fetched.HasValue.ShouldBeTrue();
          fetched.Value.AgentId.ShouldBe(new AgentId("a-1"));
          fetched.Value.Skills.Single().Name.ShouldBe("skill-a");
      }

      [Fact]
      public async Task FindBySkillAsync_ReturnsAgentsAdvertisingThatSkill()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          await store.RegisterAsync(Sample("a-2", "summarize-pdf"));
          await store.RegisterAsync(Sample("a-3", "transcribe-audio"));

          var matches = await store.FindBySkillAsync("summarize-pdf");
          matches.Select(x => x.AgentId.Value).ShouldContain("a-2");
          matches.Select(x => x.AgentId.Value).ShouldNotContain("a-3");
      }

      [Fact]
      public async Task RegisterAsync_RejectsDuplicateSkillNameWithinAgent()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          var reg = Sample("a-4") with
          {
              Skills = new[]
              {
                  new Skill { Name = "dup", Description = "1" },
                  new Skill { Name = "dup", Description = "2" }
              }
          };

          var result = await store.RegisterAsync(reg);
          result.IsFailure.ShouldBeTrue();
          result.Error.ShouldBe(StorageErrors.ValidationSkillDuplicate);
      }

      [Fact]
      public async Task RegisterAsync_RejectsSkillRequiringUnknownTool()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          var reg = Sample("a-5") with
          {
              Skills = new[]
              {
                  new Skill { Name = "needs-tool", Description = "x", RequiredTools = new[] { "missing-tool" } }
              },
              Tools = Array.Empty<ToolBinding>()
          };

          var result = await store.RegisterAsync(reg);
          result.IsFailure.ShouldBeTrue();
          result.Error.ShouldBe(StorageErrors.ValidationSkillRequiresUnknownTool);
      }

      [Fact]
      public async Task UpdateHeartbeatAsync_BumpsLastHeartbeat()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          await store.RegisterAsync(Sample("a-6"));

          var before = (await store.GetAsync(new AgentId("a-6"))).Value.LastHeartbeat;
          await Task.Delay(10);
          var beat = await store.UpdateHeartbeatAsync(new AgentId("a-6"));
          beat.IsSuccess.ShouldBeTrue();

          var after = (await store.GetAsync(new AgentId("a-6"))).Value.LastHeartbeat;
          after.ShouldBeGreaterThan(before);
      }

      [Fact]
      public async Task UpdateStatusAsync_UpdatesStatus()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          await store.RegisterAsync(Sample("a-7"));

          var result = await store.UpdateStatusAsync(new AgentId("a-7"), AgentStatus.Healthy);
          result.IsSuccess.ShouldBeTrue();

          (await store.GetAsync(new AgentId("a-7"))).Value.Status.ShouldBe(AgentStatus.Healthy);
      }

      [Fact]
      public async Task GetAllAsync_ReturnsAllRegistered()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAgentRegistrationStore(ctx);
          await store.RegisterAsync(Sample("a-8"));
          await store.RegisterAsync(Sample("a-9"));

          var all = await store.GetAllAsync();
          all.Select(x => x.AgentId.Value).ShouldContain("a-8");
          all.Select(x => x.AgentId.Value).ShouldContain("a-9");
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfAgentRegistrationStoreTests"`
  Expected: FAIL — `EfAgentRegistrationStore` doesn't exist.

- [ ] **Step 3: Write `EfAgentRegistrationStore`**

  ```csharp
  using CSharpFunctionalExtensions;
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Identity;
  using Sigil.Core.Registry;
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public sealed class EfAgentRegistrationStore : IAgentRegistrationStore
  {
      private readonly SigilDbContext _ctx;
      public EfAgentRegistrationStore(SigilDbContext ctx) => _ctx = ctx;

      public async Task<Result> RegisterAsync(AgentRegistration registration, CancellationToken ct = default)
      {
          var validation = Validate(registration);
          if (validation.IsFailure) return validation;

          var existing = await _ctx.AgentRegistrations.FindAsync(new object?[] { registration.AgentId }, ct);
          if (existing is not null) return Result.Failure(StorageErrors.DuplicateAgent);

          _ctx.AgentRegistrations.Add(registration);
          await _ctx.SaveChangesAsync(ct);
          return Result.Success();
      }

      public async Task<Maybe<AgentRegistration>> GetAsync(AgentId agentId, CancellationToken ct = default)
      {
          var found = await _ctx.AgentRegistrations
              .AsNoTracking()
              .FirstOrDefaultAsync(x => x.AgentId == agentId, ct);
          return found is null ? Maybe<AgentRegistration>.None : Maybe.From(found);
      }

      public async Task<IReadOnlyList<AgentRegistration>> GetAllAsync(CancellationToken ct = default) =>
          await _ctx.AgentRegistrations.AsNoTracking().ToListAsync(ct);

      public async Task<IReadOnlyList<AgentRegistration>> FindBySkillAsync(string skillName, CancellationToken ct = default)
      {
          // Pull all then filter in-memory: works on every provider, doesn't depend on
          // jsonb-specific operators. The GIN index from the migration optimizes the
          // future server-side variant; for v1 traffic levels (a handful of agents),
          // in-memory filtering is fine. Replace with a server-side jsonb_path query
          // if registry size grows.
          var all = await _ctx.AgentRegistrations.AsNoTracking().ToListAsync(ct);
          return all.Where(a => a.Skills.Any(s => s.Name == skillName)).ToList();
      }

      public async Task<IReadOnlyList<AgentRegistration>> FindByDomainAsync(string domain, CancellationToken ct = default) =>
          await _ctx.AgentRegistrations.AsNoTracking()
              .Where(x => x.Domain == domain)
              .ToListAsync(ct);

      public async Task<Result> UpdateHeartbeatAsync(AgentId agentId, CancellationToken ct = default)
      {
          var rows = await _ctx.AgentRegistrations
              .Where(x => x.AgentId == agentId)
              .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastHeartbeat, DateTime.UtcNow), ct);
          return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
      }

      public async Task<Result> UpdateStatusAsync(AgentId agentId, AgentStatus status, CancellationToken ct = default)
      {
          var rows = await _ctx.AgentRegistrations
              .Where(x => x.AgentId == agentId)
              .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status), ct);
          return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
      }

      private static Result Validate(AgentRegistration reg)
      {
          if (reg.Skills.Any(s => string.IsNullOrWhiteSpace(s.Name)))
              return Result.Failure(StorageErrors.ValidationSkillName);

          var skillNames = reg.Skills.Select(s => s.Name).ToList();
          if (skillNames.Distinct(StringComparer.Ordinal).Count() != skillNames.Count)
              return Result.Failure(StorageErrors.ValidationSkillDuplicate);

          var toolNames = reg.Tools.Select(t => t.Name).ToList();
          if (toolNames.Distinct(StringComparer.Ordinal).Count() != toolNames.Count)
              return Result.Failure(StorageErrors.ValidationToolNameDuplicate);

          var toolSet = toolNames.ToHashSet(StringComparer.Ordinal);
          foreach (var skill in reg.Skills)
              foreach (var required in skill.RequiredTools)
                  if (!toolSet.Contains(required))
                      return Result.Failure(StorageErrors.ValidationSkillRequiresUnknownTool);

          foreach (var allowed in reg.Security.AllowedTools)
              if (!toolSet.Contains(allowed))
                  return Result.Failure(StorageErrors.ValidationAllowedToolUnknown);

          return Result.Success();
      }
  }
  ```

- [ ] **Step 4: Run the integration tests against the live container**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfAgentRegistrationStoreTests"`
  Expected: all seven tests pass. The first run pulls the `postgres:16-alpine` image — allow up to a minute on a cold cache.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfAgentRegistrationStore.cs tests/Sigil.Storage.EfCore.Tests/EfAgentRegistrationStoreTests.cs
  git commit -m "feat(storage): EfAgentRegistrationStore with §2.8 validation"
  ```

---

## Task 17: `EfJobStore` + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfJobStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfJobStoreTests.cs`

`EfJobStore.CreateAsync` writes the `Job` and a paired empty `ContextStateRecord` in the same `SaveChangesAsync` so `GetSnapshotAsync` always finds a row.

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Shouldly;
  using Sigil.Core.Identity;
  using Sigil.Core.Jobs;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfJobStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfJobStoreTests(PostgresFixture pg) => _pg = pg;

      [Fact]
      public async Task CreateAsync_PersistsJobAndPairedContextStateRow()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfJobStore(ctx);
          var job = new Job { JobId = new JobId("job-1") };

          var result = await store.CreateAsync(job);
          result.IsSuccess.ShouldBeTrue();

          await using var verify = _pg.NewContext();
          var stored = await verify.Jobs.FindAsync(new JobId("job-1"));
          stored.ShouldNotBeNull();
          stored!.Status.ShouldBe(JobStatus.Pending);

          var ctxRow = await verify.ContextStates.FirstOrDefaultAsync(x => x.JobId == "job-1");
          ctxRow.ShouldNotBeNull("EfJobStore.CreateAsync must seed an empty ContextStateRecord");
          ctxRow!.ETag.ShouldNotBeNullOrEmpty();
          ctxRow.State.ShouldBeEmpty();
      }

      [Fact]
      public async Task GetAsync_ReturnsExistingJob()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfJobStore(ctx);
          await store.CreateAsync(new Job { JobId = new JobId("job-2") });

          var fetched = await store.GetAsync(new JobId("job-2"));
          fetched.HasValue.ShouldBeTrue();
      }

      [Fact]
      public async Task UpdateStatusAsync_ChangesStatus()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfJobStore(ctx);
          await store.CreateAsync(new Job { JobId = new JobId("job-3") });

          var result = await store.UpdateStatusAsync(new JobId("job-3"), JobStatus.Completed);
          result.IsSuccess.ShouldBeTrue();
          (await store.GetAsync(new JobId("job-3"))).Value.Status.ShouldBe(JobStatus.Completed);
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfJobStoreTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `EfJobStore`**

  ```csharp
  using CSharpFunctionalExtensions;
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Identity;
  using Sigil.Core.Jobs;
  using Sigil.Core.Storage;
  using Sigil.Storage.EfCore.Persistence;

  namespace Sigil.Storage.EfCore;

  public sealed class EfJobStore : IJobStore
  {
      private readonly SigilDbContext _ctx;
      public EfJobStore(SigilDbContext ctx) => _ctx = ctx;

      public async Task<Result> CreateAsync(Job job, CancellationToken ct = default)
      {
          var existing = await _ctx.Jobs.FindAsync(new object?[] { job.JobId }, ct);
          if (existing is not null) return Result.Failure(StorageErrors.NotFound);

          _ctx.Jobs.Add(job);
          _ctx.ContextStates.Add(new ContextStateRecord
          {
              JobId = job.JobId.Value,
              ETag = Guid.NewGuid().ToString("N"),
              State = new Dictionary<string, object>(),
              Log = new List<Sigil.Core.Protocol.AgentLogEntry>()
          });
          await _ctx.SaveChangesAsync(ct);
          return Result.Success();
      }

      public async Task<Maybe<Job>> GetAsync(JobId jobId, CancellationToken ct = default)
      {
          var found = await _ctx.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.JobId == jobId, ct);
          return found is null ? Maybe<Job>.None : Maybe.From(found);
      }

      public async Task<Result> UpdateStatusAsync(JobId jobId, JobStatus status, CancellationToken ct = default)
      {
          var rows = await _ctx.Jobs
              .Where(x => x.JobId == jobId)
              .ExecuteUpdateAsync(s => s
                  .SetProperty(x => x.Status, status)
                  .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
          return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfJobStoreTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfJobStore.cs tests/Sigil.Storage.EfCore.Tests/EfJobStoreTests.cs
  git commit -m "feat(storage): EfJobStore with paired context-state seed"
  ```

---

## Task 18: `EfContextStore` — atomic ETag-conditional commit

The headline deliverable. `CommitDeltaAsync` must atomically: read current `(state, etag)`, fail if `etag != expected`, otherwise apply delta and write a new etag — all without races. Implementation: read for merge, then `ExecuteUpdateAsync` with `Where(etag == expected)`. Row-count is the conflict signal.

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfContextStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfContextStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Shouldly;
  using Sigil.Core.Identity;
  using Sigil.Core.Jobs;
  using Sigil.Core.Protocol;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfContextStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfContextStoreTests(PostgresFixture pg) => _pg = pg;

      private async Task<JobId> SeedJob(string id)
      {
          await using var ctx = _pg.NewContext();
          var store = new EfJobStore(ctx);
          var jobId = new JobId(id);
          await store.CreateAsync(new Job { JobId = jobId });
          return jobId;
      }

      [Fact]
      public async Task GetSnapshotAsync_ReturnsEmptySnapshotForNewJob()
      {
          var jobId = await SeedJob("ctx-1");
          await using var ctx = _pg.NewContext();
          var store = new EfContextStore(ctx);

          var result = await store.GetSnapshotAsync(jobId);
          result.IsSuccess.ShouldBeTrue();
          result.Value.Snapshot.State.ShouldBeEmpty();
          result.Value.ETag.Value.ShouldNotBeNullOrEmpty();
      }

      [Fact]
      public async Task CommitDeltaAsync_AppliesUpdates_WhenETagMatches()
      {
          var jobId = await SeedJob("ctx-2");

          ETag firstETag;
          await using (var ctx1 = _pg.NewContext())
          {
              var snap = await new EfContextStore(ctx1).GetSnapshotAsync(jobId);
              firstETag = snap.Value.ETag;
          }

          await using (var ctx2 = _pg.NewContext())
          {
              var commit = await new EfContextStore(ctx2).CommitDeltaAsync(
                  jobId,
                  new ContextDelta { Updates = new() { ["count"] = 1 } },
                  firstETag);
              commit.IsSuccess.ShouldBeTrue();
          }

          await using (var ctx3 = _pg.NewContext())
          {
              var after = await new EfContextStore(ctx3).GetSnapshotAsync(jobId);
              after.Value.Snapshot.State["count"].ToString().ShouldBe("1");
              after.Value.ETag.ShouldNotBe(firstETag);
          }
      }

      [Fact]
      public async Task CommitDeltaAsync_ReturnsFailure_OnETagMismatch()
      {
          var jobId = await SeedJob("ctx-3");

          await using var ctx = _pg.NewContext();
          var store = new EfContextStore(ctx);
          var staleETag = new ETag("stale-tag-that-doesnt-match");

          var commit = await store.CommitDeltaAsync(
              jobId,
              new ContextDelta { Updates = new() { ["x"] = 1 } },
              staleETag);

          commit.IsFailure.ShouldBeTrue();
          commit.Error.ShouldBe(StorageErrors.EtagMismatch);
      }

      [Fact]
      public async Task ParallelCommits_OnlyOneSucceeds()
      {
          var jobId = await SeedJob("ctx-4");

          ETag etag;
          await using (var ctx0 = _pg.NewContext())
          {
              etag = (await new EfContextStore(ctx0).GetSnapshotAsync(jobId)).Value.ETag;
          }

          var t1 = Task.Run(async () =>
          {
              await using var c = _pg.NewContext();
              return await new EfContextStore(c).CommitDeltaAsync(
                  jobId,
                  new ContextDelta { Updates = new() { ["who"] = "A" } },
                  etag);
          });
          var t2 = Task.Run(async () =>
          {
              await using var c = _pg.NewContext();
              return await new EfContextStore(c).CommitDeltaAsync(
                  jobId,
                  new ContextDelta { Updates = new() { ["who"] = "B" } },
                  etag);
          });

          var results = await Task.WhenAll(t1, t2);
          results.Count(r => r.IsSuccess).ShouldBe(1);
          results.Count(r => r.IsFailure && r.Error == StorageErrors.EtagMismatch).ShouldBe(1);
      }

      [Fact]
      public async Task CommitDeltaAsync_AppliesRemovals()
      {
          var jobId = await SeedJob("ctx-5");

          await using (var ctx1 = _pg.NewContext())
          {
              var s = new EfContextStore(ctx1);
              var snap = await s.GetSnapshotAsync(jobId);
              await s.CommitDeltaAsync(
                  jobId,
                  new ContextDelta { Updates = new() { ["a"] = 1, ["b"] = 2 } },
                  snap.Value.ETag);
          }

          await using (var ctx2 = _pg.NewContext())
          {
              var s = new EfContextStore(ctx2);
              var snap = await s.GetSnapshotAsync(jobId);
              var removed = await s.CommitDeltaAsync(
                  jobId,
                  new ContextDelta { Removals = new[] { "a" } },
                  snap.Value.ETag);
              removed.IsSuccess.ShouldBeTrue();
          }

          await using (var ctx3 = _pg.NewContext())
          {
              var snap = await new EfContextStore(ctx3).GetSnapshotAsync(jobId);
              snap.Value.Snapshot.State.ContainsKey("a").ShouldBeFalse();
              snap.Value.Snapshot.State.ContainsKey("b").ShouldBeTrue();
          }
      }

      [Fact]
      public async Task AppendLogAsync_AppendsAndGetLogReturnsInOrder()
      {
          var jobId = await SeedJob("ctx-6");

          await using var ctx = _pg.NewContext();
          var store = new EfContextStore(ctx);
          await store.AppendLogAsync(jobId, new AgentLogEntry { Message = "first", AgentId = new AgentId("a") });
          await store.AppendLogAsync(jobId, new AgentLogEntry { Message = "second", AgentId = new AgentId("a") });

          await using var ctx2 = _pg.NewContext();
          var log = await new EfContextStore(ctx2).GetLogAsync(jobId);
          log.Select(x => x.Message).ShouldBe(new[] { "first", "second" });
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfContextStoreTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `EfContextStore`**

  ```csharp
  using CSharpFunctionalExtensions;
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Identity;
  using Sigil.Core.Protocol;
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public sealed class EfContextStore : IContextStore
  {
      private readonly SigilDbContext _ctx;
      public EfContextStore(SigilDbContext ctx) => _ctx = ctx;

      public async Task<Result<(ContextSnapshot Snapshot, ETag ETag)>> GetSnapshotAsync(
          JobId jobId, CancellationToken ct = default)
      {
          var row = await _ctx.ContextStates.AsNoTracking()
              .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
          if (row is null)
              return Result.Failure<(ContextSnapshot, ETag)>(StorageErrors.NotFound);

          var snapshot = new ContextSnapshot { JobId = jobId, State = row.State };
          return Result.Success((snapshot, new ETag(row.ETag)));
      }

      public async Task<Result> CommitDeltaAsync(
          JobId jobId,
          ContextDelta delta,
          ETag expectedETag,
          CancellationToken ct = default)
      {
          // Read current state for merge. AsNoTracking — we'll write via ExecuteUpdate.
          var row = await _ctx.ContextStates.AsNoTracking()
              .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
          if (row is null) return Result.Failure(StorageErrors.NotFound);
          if (row.ETag != expectedETag.Value) return Result.Failure(StorageErrors.EtagMismatch);

          var nextState = ApplyDelta(row.State, delta);
          var nextETag = Guid.NewGuid().ToString("N");

          var rows = await _ctx.ContextStates
              .Where(x => x.JobId == jobId.Value && x.ETag == expectedETag.Value)
              .ExecuteUpdateAsync(s => s
                  .SetProperty(x => x.State, nextState)
                  .SetProperty(x => x.ETag, nextETag), ct);

          return rows == 1
              ? Result.Success()
              : Result.Failure(StorageErrors.EtagMismatch);
      }

      public async Task AppendLogAsync(JobId jobId, AgentLogEntry entry, CancellationToken ct = default)
      {
          // Append within a serializable transaction so concurrent appends don't lose entries.
          await using var tx = await _ctx.Database.BeginTransactionAsync(
              System.Data.IsolationLevel.Serializable, ct);

          var row = await _ctx.ContextStates
              .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
          if (row is null) throw new InvalidOperationException(
              $"No context state for job {jobId.Value}; was JobStore.CreateAsync called?");

          row.Log = new List<AgentLogEntry>(row.Log) { entry };
          await _ctx.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
      }

      public async Task<IReadOnlyList<AgentLogEntry>> GetLogAsync(JobId jobId, CancellationToken ct = default)
      {
          var row = await _ctx.ContextStates.AsNoTracking()
              .FirstOrDefaultAsync(x => x.JobId == jobId.Value, ct);
          return row is null ? Array.Empty<AgentLogEntry>() : row.Log;
      }

      private static IReadOnlyDictionary<string, object> ApplyDelta(
          IReadOnlyDictionary<string, object> current, ContextDelta delta)
      {
          var merged = new Dictionary<string, object>(current);
          foreach (var k in delta.Removals) merged.Remove(k);
          foreach (var (k, v) in delta.Updates) merged[k] = v;
          return merged;
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfContextStoreTests"`
  Expected: all six tests pass — including the parallel-commits race test (one success, one `etag-mismatch`).

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfContextStore.cs tests/Sigil.Storage.EfCore.Tests/EfContextStoreTests.cs
  git commit -m "feat(storage): EfContextStore with atomic ETag-conditional commit"
  ```

---

## Task 19: `EfCheckpointStore` + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfCheckpointStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfCheckpointStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Shouldly;
  using Sigil.Core.Checkpoints;
  using Sigil.Core.Identity;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfCheckpointStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfCheckpointStoreTests(PostgresFixture pg) => _pg = pg;

      [Fact]
      public async Task CreateAsync_PersistsCheckpoint()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfCheckpointStore(ctx);
          var cp = new Checkpoint { JobId = new JobId("cp-1"), StepId = new StepId("step-1") };

          var result = await store.CreateAsync(cp);
          result.IsSuccess.ShouldBeTrue();

          var fetched = await store.GetAsync(cp.CheckpointId);
          fetched.HasValue.ShouldBeTrue();
          fetched.Value.JobId.ShouldBe(new JobId("cp-1"));
      }

      [Fact]
      public async Task ResolveAsync_SetsStatusAndResolver()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfCheckpointStore(ctx);
          var cp = new Checkpoint { JobId = new JobId("cp-2"), StepId = new StepId("step-2") };
          await store.CreateAsync(cp);

          var resolved = await store.ResolveAsync(cp.CheckpointId, CheckpointStatus.Approved, "alice");
          resolved.IsSuccess.ShouldBeTrue();

          var fetched = (await store.GetAsync(cp.CheckpointId)).Value;
          fetched.Status.ShouldBe(CheckpointStatus.Approved);
          fetched.ResolvedBy.ShouldBe("alice");
          fetched.ResolvedAt.ShouldNotBeNull();
      }

      [Fact]
      public async Task GetPendingForJobAsync_ReturnsOnlyPending()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfCheckpointStore(ctx);
          var pending = new Checkpoint { JobId = new JobId("cp-3"), StepId = new StepId("s1") };
          var resolved = new Checkpoint { JobId = new JobId("cp-3"), StepId = new StepId("s2") };
          await store.CreateAsync(pending);
          await store.CreateAsync(resolved);
          await store.ResolveAsync(resolved.CheckpointId, CheckpointStatus.Approved, "bob");

          var found = await store.GetPendingForJobAsync(new JobId("cp-3"));
          found.Select(x => x.CheckpointId).ShouldContain(pending.CheckpointId);
          found.Select(x => x.CheckpointId).ShouldNotContain(resolved.CheckpointId);
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfCheckpointStoreTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `EfCheckpointStore`**

  ```csharp
  using CSharpFunctionalExtensions;
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Checkpoints;
  using Sigil.Core.Identity;
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public sealed class EfCheckpointStore : ICheckpointStore
  {
      private readonly SigilDbContext _ctx;
      public EfCheckpointStore(SigilDbContext ctx) => _ctx = ctx;

      public async Task<Result> CreateAsync(Checkpoint checkpoint, CancellationToken ct = default)
      {
          _ctx.Checkpoints.Add(checkpoint);
          await _ctx.SaveChangesAsync(ct);
          return Result.Success();
      }

      public async Task<Maybe<Checkpoint>> GetAsync(string checkpointId, CancellationToken ct = default)
      {
          var found = await _ctx.Checkpoints.AsNoTracking()
              .FirstOrDefaultAsync(x => x.CheckpointId == checkpointId, ct);
          return found is null ? Maybe<Checkpoint>.None : Maybe.From(found);
      }

      public async Task<IReadOnlyList<Checkpoint>> GetPendingForJobAsync(JobId jobId, CancellationToken ct = default) =>
          await _ctx.Checkpoints.AsNoTracking()
              .Where(x => x.JobId == jobId && x.Status == CheckpointStatus.Pending)
              .ToListAsync(ct);

      public async Task<Result> ResolveAsync(
          string checkpointId,
          CheckpointStatus status,
          string resolvedBy,
          CancellationToken ct = default)
      {
          var rows = await _ctx.Checkpoints
              .Where(x => x.CheckpointId == checkpointId && x.Status == CheckpointStatus.Pending)
              .ExecuteUpdateAsync(s => s
                  .SetProperty(x => x.Status, status)
                  .SetProperty(x => x.ResolvedBy, resolvedBy)
                  .SetProperty(x => x.ResolvedAt, DateTime.UtcNow), ct);
          return rows == 1 ? Result.Success() : Result.Failure(StorageErrors.NotFound);
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfCheckpointStoreTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfCheckpointStore.cs tests/Sigil.Storage.EfCore.Tests/EfCheckpointStoreTests.cs
  git commit -m "feat(storage): EfCheckpointStore"
  ```

---

## Task 20: `EfAuditStore` (append-only) + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfAuditStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfAuditStoreTests.cs`

The store has `Add` only — no `Update` / `Delete` paths. Two `LogChangeAsync` calls with identical content must yield two distinct rows (different `AuditId`).

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Shouldly;
  using Sigil.Core.Audit;
  using Sigil.Core.Identity;
  using Sigil.Core.Protocol;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfAuditStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfAuditStoreTests(PostgresFixture pg) => _pg = pg;

      [Fact]
      public async Task LogChangeAsync_PersistsTwoRowsForIdenticalContent()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAuditStore(ctx);
          var jobId = new JobId("audit-1");
          var agentId = new AgentId("a");
          var stepId = new StepId("s");

          var entry1 = new AuditEntry { JobId = jobId, AgentId = agentId, StepId = stepId };
          var entry2 = new AuditEntry { JobId = jobId, AgentId = agentId, StepId = stepId };
          await store.LogChangeAsync(entry1);
          await store.LogChangeAsync(entry2);

          var history = await store.GetHistoryAsync(jobId);
          history.Count.ShouldBe(2);
          history.Select(x => x.AuditId).ToHashSet().Count.ShouldBe(2);
      }

      [Fact]
      public async Task GetHistoryAsync_FiltersByJob()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAuditStore(ctx);
          await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-2"), AgentId = new AgentId("a"), StepId = new StepId("s") });
          await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-3"), AgentId = new AgentId("a"), StepId = new StepId("s") });

          var history = await store.GetHistoryAsync(new JobId("audit-2"));
          history.Count.ShouldBe(1);
          history[0].JobId.ShouldBe(new JobId("audit-2"));
      }

      [Fact]
      public async Task GetAgentHistoryAsync_FiltersByAgent()
      {
          await using var ctx = _pg.NewContext();
          var store = new EfAuditStore(ctx);
          await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-4"), AgentId = new AgentId("zeta"), StepId = new StepId("s") });
          await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-5"), AgentId = new AgentId("zeta"), StepId = new StepId("s") });
          await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-6"), AgentId = new AgentId("other"), StepId = new StepId("s") });

          var history = await store.GetAgentHistoryAsync(new AgentId("zeta"));
          history.Count.ShouldBe(2);
          history.ShouldAllBe(x => x.AgentId == new AgentId("zeta"));
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfAuditStoreTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `EfAuditStore`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Sigil.Core.Audit;
  using Sigil.Core.Identity;
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public sealed class EfAuditStore : IAuditStore
  {
      private readonly SigilDbContext _ctx;
      public EfAuditStore(SigilDbContext ctx) => _ctx = ctx;

      public async Task LogChangeAsync(AuditEntry entry, CancellationToken ct = default)
      {
          _ctx.AuditEntries.Add(entry);
          await _ctx.SaveChangesAsync(ct);
      }

      public async Task<IReadOnlyList<AuditEntry>> GetHistoryAsync(JobId jobId, CancellationToken ct = default) =>
          await _ctx.AuditEntries.AsNoTracking()
              .Where(x => x.JobId == jobId)
              .OrderBy(x => x.Timestamp)
              .ToListAsync(ct);

      public async Task<IReadOnlyList<AuditEntry>> GetAgentHistoryAsync(AgentId agentId, CancellationToken ct = default) =>
          await _ctx.AuditEntries.AsNoTracking()
              .Where(x => x.AgentId == agentId)
              .OrderBy(x => x.Timestamp)
              .ToListAsync(ct);
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfAuditStoreTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfAuditStore.cs tests/Sigil.Storage.EfCore.Tests/EfAuditStoreTests.cs
  git commit -m "feat(storage): EfAuditStore (append-only)"
  ```

---

## Task 21: `EfSigilStore` aggregator + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/EfSigilStore.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/EfSigilStoreTests.cs`

- [ ] **Step 1: Write the failing test**

  ```csharp
  using Shouldly;
  using Sigil.Core.Storage;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class EfSigilStoreTests
  {
      private readonly PostgresFixture _pg;
      public EfSigilStoreTests(PostgresFixture pg) => _pg = pg;

      [Fact]
      public void EfSigilStore_ExposesAllFiveSubStores()
      {
          using var ctx = _pg.NewContext();
          ISigilStore store = new EfSigilStore(
              new EfAgentRegistrationStore(ctx),
              new EfJobStore(ctx),
              new EfContextStore(ctx),
              new EfCheckpointStore(ctx),
              new EfAuditStore(ctx));

          store.Agents.ShouldNotBeNull();
          store.Jobs.ShouldNotBeNull();
          store.Contexts.ShouldNotBeNull();
          store.Checkpoints.ShouldNotBeNull();
          store.Audit.ShouldNotBeNull();
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfSigilStoreTests"`
  Expected: FAIL — `EfSigilStore` not defined.

- [ ] **Step 3: Write `EfSigilStore`**

  ```csharp
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public sealed class EfSigilStore : ISigilStore
  {
      public EfSigilStore(
          IAgentRegistrationStore agents,
          IJobStore jobs,
          IContextStore contexts,
          ICheckpointStore checkpoints,
          IAuditStore audit)
      {
          Agents = agents;
          Jobs = jobs;
          Contexts = contexts;
          Checkpoints = checkpoints;
          Audit = audit;
      }

      public IAgentRegistrationStore Agents { get; }
      public IJobStore Jobs { get; }
      public IContextStore Contexts { get; }
      public ICheckpointStore Checkpoints { get; }
      public IAuditStore Audit { get; }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~EfSigilStoreTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/EfSigilStore.cs tests/Sigil.Storage.EfCore.Tests/EfSigilStoreTests.cs
  git commit -m "feat(storage): EfSigilStore aggregator"
  ```

---

## Task 22: `AddSigilEfCore` DI extension + tests

**Files:**
- Create: `src/Sigil.Storage.EfCore/ServiceCollectionExtensions.cs`
- Create: `tests/Sigil.Storage.EfCore.Tests/AddSigilEfCoreTests.cs`

- [ ] **Step 1: Write the failing tests**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging;
  using Microsoft.Extensions.Logging.Abstractions;
  using Microsoft.Extensions.Options;
  using Shouldly;
  using Sigil.Core.Storage;
  using Sigil.Storage.EfCore;
  using Sigil.Storage.EfCore.Tests.Infrastructure;
  using Xunit;

  namespace Sigil.Storage.EfCore.Tests;

  [Collection("SigilDb")]
  public class AddSigilEfCoreTests
  {
      private readonly PostgresFixture _pg;
      public AddSigilEfCoreTests(PostgresFixture pg) => _pg = pg;

      private IConfiguration BuildConfig() =>
          new ConfigurationBuilder()
              .AddInMemoryCollection(new Dictionary<string, string?>
              {
                  ["Storage:EfCore:ConnectionString"] = _pg.ConnectionString
              })
              .Build();

      private static IServiceCollection NewServices()
          => new ServiceCollection()
              .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

      [Fact]
      public void Resolves_ISigilStore_As_EfSigilStore()
      {
          using var provider = NewServices()
              .AddSigilEfCore(BuildConfig())
              .BuildServiceProvider();

          using var scope = provider.CreateScope();
          var resolved = scope.ServiceProvider.GetRequiredService<ISigilStore>();
          resolved.ShouldBeOfType<EfSigilStore>();
      }

      [Fact]
      public void Resolves_AllFiveSubStores_AsScoped()
      {
          using var provider = NewServices()
              .AddSigilEfCore(BuildConfig())
              .BuildServiceProvider();

          using var scope = provider.CreateScope();
          var sp = scope.ServiceProvider;
          sp.GetRequiredService<IAgentRegistrationStore>().ShouldBeOfType<EfAgentRegistrationStore>();
          sp.GetRequiredService<IJobStore>().ShouldBeOfType<EfJobStore>();
          sp.GetRequiredService<IContextStore>().ShouldBeOfType<EfContextStore>();
          sp.GetRequiredService<ICheckpointStore>().ShouldBeOfType<EfCheckpointStore>();
          sp.GetRequiredService<IAuditStore>().ShouldBeOfType<EfAuditStore>();
      }

      [Fact]
      public void Options_BindFromConfiguration()
      {
          using var provider = NewServices()
              .AddSigilEfCore(BuildConfig())
              .BuildServiceProvider();

          var opts = provider.GetRequiredService<IOptions<SigilEfCoreOptions>>().Value;
          opts.ConnectionString.ShouldBe(_pg.ConnectionString);
      }
  }
  ```

- [ ] **Step 2: Run to verify failure**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AddSigilEfCoreTests"`
  Expected: FAIL.

- [ ] **Step 3: Write `ServiceCollectionExtensions`**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using Sigil.Core.Storage;

  namespace Sigil.Storage.EfCore;

  public static class ServiceCollectionExtensions
  {
      public static IServiceCollection AddSigilEfCore(
          this IServiceCollection services,
          IConfiguration configuration)
      {
          services
              .AddOptions<SigilEfCoreOptions>()
              .Bind(configuration.GetSection(SigilEfCoreOptions.SectionName))
              .ValidateOnStart();

          services.AddDbContext<SigilDbContext>((sp, opts) =>
          {
              var settings = sp.GetRequiredService<
                  Microsoft.Extensions.Options.IOptions<SigilEfCoreOptions>>().Value;
              opts.UseNpgsql(settings.ConnectionString);
          });

          services.AddScoped<IAgentRegistrationStore, EfAgentRegistrationStore>();
          services.AddScoped<IJobStore, EfJobStore>();
          services.AddScoped<IContextStore, EfContextStore>();
          services.AddScoped<ICheckpointStore, EfCheckpointStore>();
          services.AddScoped<IAuditStore, EfAuditStore>();

          services.AddScoped<ISigilStore>(sp => new EfSigilStore(
              sp.GetRequiredService<IAgentRegistrationStore>(),
              sp.GetRequiredService<IJobStore>(),
              sp.GetRequiredService<IContextStore>(),
              sp.GetRequiredService<ICheckpointStore>(),
              sp.GetRequiredService<IAuditStore>()));

          return services;
      }
  }
  ```

- [ ] **Step 4: Run to verify pass**

  Run: `dotnet test tests/Sigil.Storage.EfCore.Tests --filter "FullyQualifiedName~AddSigilEfCoreTests"`
  Expected: PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add src/Sigil.Storage.EfCore/ServiceCollectionExtensions.cs tests/Sigil.Storage.EfCore.Tests/AddSigilEfCoreTests.cs
  git commit -m "feat(storage): AddSigilEfCore DI extension"
  ```

---

## Task 23: Doc updates — flip the v1-default annotation to EF Core

**Files:**
- Modify: `Roadmap.md`
- Modify: `.bob/docs/sigil-architecture-blueprint.md`
- Modify: `README.md`

These edits move the "v1 default / used by Docker Compose" position from MongoDB to EF Core, in keeping with the project decision recorded in the user memory file `project_default_store_efcore.md`. **No before/after framing** — describe the present state.

- [ ] **Step 1: Update `Roadmap.md`**

  Find the `## Phase 1 · Layer 2 — Storage` section. Replace the table body so the EF Core row carries the *(used by v1 Docker Compose)* annotation:

  ```markdown
  ## Phase 1 · Layer 2 — Storage *(parallelizable)*

  | Status | Issue | Title |
  |---|---|---|
  | ⬜ | [#6](https://github.com/satish-krishna/sigil/issues/6) | EF Core provider + initial migration *(used by v1 Docker Compose)* |
  | ⬜ | [#5](https://github.com/satish-krishna/sigil/issues/5) | MongoDB provider with ETag concurrency |
  ```

- [ ] **Step 2: Update `.bob/docs/sigil-architecture-blueprint.md` §4.8 consumer-registration code**

  Around line 1037–1056, swap the order so EF Core appears as Option A:

  ```csharp
  // Option A: EF Core (Postgres) — default
  builder.AddSigil(sigil =>
  {
      sigil.UseEfCore(options =>
      {
          options.UseNpgsql("Host=localhost;Database=sigil");
      });
  });

  // Option B: MongoDB
  builder.AddSigil(sigil =>
  {
      sigil.UseMongo(options =>
      {
          options.ConnectionString = "mongodb://localhost:27017";
          options.Database = "sigil";
      });
  });
  ```

- [ ] **Step 3: Update §9 phase plan ordering**

  Find these two consecutive lines (around line 1454–1455):

  ```markdown
  - [ ] MongoDB storage provider with ETag support
  - [ ] EF Core storage provider with initial migration
  ```

  Reorder so EF Core comes first:

  ```markdown
  - [ ] EF Core storage provider with initial migration
  - [ ] MongoDB storage provider with ETag support
  ```

- [ ] **Step 4: Update `README.md`**

  Search `README.md` for `MongoDB` mentions in any v1-default context. Specifically:

  - In the *Stack* section, the line currently reads `**Storage** — \`ISigilStore\` abstraction with two providers: MongoDB and EF Core`. Reorder to `**Storage** — \`ISigilStore\` abstraction with two providers: EF Core (Postgres, default) and MongoDB`.
  - In the *Status* checklist, the bullet currently says `MongoDB and EF Core storage providers` — leave the bullet content but ensure no surrounding text implies Mongo is the default.
  - The "Full local stack" comment block currently mentions `# Full local stack — kernel + MongoDB + sample agent (later)`. Replace `MongoDB` with `Postgres`.

  Run: `grep -n "MongoDB\|Mongo" README.md` and review every hit.

- [ ] **Step 5: Verify build still green and docs compile in any tooling that processes them**

  Run: `dotnet build sigil.sln`
  Expected: clean.

- [ ] **Step 6: Commit**

  ```bash
  git add Roadmap.md .bob/docs/sigil-architecture-blueprint.md README.md
  git commit -m "docs: position EF Core as the v1 default ISigilStore provider"
  ```

---

## Task 24: Final solution-wide test sweep + PR-ready check

- [ ] **Step 1: Full build**

  Run: `dotnet build sigil.sln`
  Expected: clean — no warnings.

- [ ] **Step 2: Full test run**

  Run: `dotnet test sigil.sln`
  Expected: every test passes — including the previously existing `Sigil.Core.Tests` and `Sigil.Infrastructure.Tests` plus the new `Sigil.Storage.EfCore.Tests`.

- [ ] **Step 3: Push the branch**

  ```bash
  git push -u origin feat/efcore-provider
  ```

- [ ] **Step 4: Open the PR**

  ```bash
  gh pr create --title "feat(storage): EF Core provider + initial migration (closes #6)" --body "$(cat <<'EOF'
  ## Summary

  - `Sigil.Storage.EfCore` lands as the default `ISigilStore` provider (Postgres via Npgsql).
  - Atomic ETag-conditional context-delta commit using `ExecuteUpdateAsync` with a `Where(etag == expected)` filter — row-count is the conflict signal.
  - All five sub-stores (`EfAgentRegistrationStore`, `EfJobStore`, `EfContextStore`, `EfCheckpointStore`, `EfAuditStore`) and the `EfSigilStore` aggregator.
  - `IServiceCollection.AddSigilEfCore(IConfiguration)` DI extension matching the existing `AddSigilSecurity` / `AddAgentGateway` shape.
  - Initial migration with GIN index on `agent_registrations.skills` for `FindBySkillAsync`.
  - Integration tests run against a live Postgres via Testcontainers (`postgres:16-alpine`).
  - Docs updated: Roadmap.md and the blueprint position EF Core as the v1 default.

  ## Test plan

  - [ ] `dotnet test sigil.sln` passes locally with Docker available.
  - [ ] Concurrency test (`EfContextStoreTests.ParallelCommits_OnlyOneSucceeds`) demonstrates the ETag race outcome.
  - [ ] `dotnet ef migrations script` runs cleanly against a fresh Postgres.
  - [ ] `dotnet build` is warning-free under `TreatWarningsAsErrors=true`.

  Closes #6.

  🤖 Generated with [Claude Code](https://claude.com/claude-code)
  EOF
  )"
  ```

  Return the PR URL once created.

---

## Self-review checklist

- [x] **Spec coverage** — Every issue #6 deliverable maps to at least one task: `SigilDbContext` (Task 9), entity configurations for `AgentRegistration` incl. owned types (Task 9), `EfSigilStore` (Task 21), `EfAuditStore` (Task 20), `EfAgentRegistrationStore.FindBySkillAsync` (Task 16), ETag concurrency (Task 18), initial migration (Task 15), `UseEfCore`/`AddSigilEfCore` extension (Task 22), Testcontainers integration test (Tasks 8 + 16-22).
- [x] **Placeholder scan** — No "TBD" / "implement later" / "similar to Task N" without code; every code-bearing step shows the code; the only "use the existing pattern" reference (Task 13's `UsageMetrics` mapping) is bracketed by a concrete instruction to read the source first.
- [x] **Type consistency** — `EfXxxStore(SigilDbContext)` constructor shape used uniformly. `StorageErrors.EtagMismatch` (not `ETagMismatch`) used consistently. `JobId.Value` (string) used at the SQL boundary; the strongly-typed `JobId` used at the API boundary.
- [x] **Docs fold-in** — Task 23 captures the Roadmap, blueprint §4.8, blueprint §9, and README updates; no separate doc PR.
