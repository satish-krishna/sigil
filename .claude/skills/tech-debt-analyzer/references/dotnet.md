# .NET Tech Debt Checklist

Reference checklist for detecting common tech debt patterns in .NET / ASP.NET Core codebases.

---

## IDisposable Misuse

### Unmanaged resources not in `using`

- **Pattern to check:** `IDisposable` objects (streams, readers, DB connections, HTTP responses) assigned to variables without `using` or `using var`.
- **Why it matters:** Leaked handles exhaust OS resources and cause intermittent failures under load.
- **Fix:** Wrap in `using var` declarations or `using (...) { }` blocks. For class-level fields, implement `IDisposable` on the owning class and dispose in `Dispose()`.

### HttpClient created with `new`

- **Pattern to check:** `new HttpClient()` inside methods, loops, or short-lived classes.
- **Why it matters:** Each instance holds a socket; rapid creation causes socket exhaustion (TIME_WAIT state) and DNS caching issues.
- **Fix:** Inject `IHttpClientFactory` or use typed/named clients registered in DI. Never `new HttpClient()` directly.

### DbContext held too long

- **Pattern to check:** `DbContext` stored as a class field in a singleton service or kept alive across multiple HTTP requests.
- **Why it matters:** DbContext is not thread-safe and accumulates tracked entities, causing memory growth and stale data.
- **Fix:** Use scoped lifetime (default). Inject `IDbContextFactory<T>` in singletons and create short-lived instances with `using`.

---

## Async/Await Anti-Patterns

### Sync-over-async (.Result / .Wait())

- **Pattern to check:** Calls to `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on tasks outside of `Main()` or top-level statements.
- **Why it matters:** Blocks the calling thread, risks deadlocks in synchronization contexts (ASP.NET, UI), and wastes thread pool threads.
- **Fix:** Propagate `async`/`await` up the entire call chain. If truly unavoidable, document the reason.

### Fire-and-forget tasks

- **Pattern to check:** `Task.Run(...)` or `_ = SomeAsync()` without awaiting, logging, or error observation.
- **Why it matters:** Unobserved exceptions crash the process (or are silently swallowed depending on config), and work may be lost on shutdown.
- **Fix:** Await the task, use `IHostedService` / background queues for out-of-band work, or at minimum observe exceptions with `ContinueWith`.

### Missing ConfigureAwait(false) in library code

- **Pattern to check:** Library/shared projects that `await` without `ConfigureAwait(false)`.
- **Why it matters:** Captures the synchronization context unnecessarily, causing deadlocks when consumed by UI or legacy ASP.NET callers.
- **Fix:** Add `ConfigureAwait(false)` to all awaits in library code. Application-level code (controllers, endpoints) can omit it.

### Async void methods

- **Pattern to check:** Methods declared `async void` other than event handlers.
- **Why it matters:** Exceptions cannot be caught by the caller, and the method cannot be awaited.
- **Fix:** Change to `async Task`. Use `async void` only for event handler signatures that require it.

---

## Null Safety

### Missing null guards on public API boundaries

- **Pattern to check:** Public methods that accept reference types without `ArgumentNullException.ThrowIfNull()` or null checks.
- **Why it matters:** Null propagates deep into the call stack, producing misleading `NullReferenceException` far from the source.
- **Fix:** Add `ArgumentNullException.ThrowIfNull(param)` at the top of public methods. Enable nullable reference types project-wide.

### Nullable reference types disabled or suppressed

- **Pattern to check:** `#nullable disable`, widespread use of `null!` or `!` (null-forgiving operator).
- **Why it matters:** Suppresses the compiler's ability to catch null dereferences at compile time.
- **Fix:** Enable `<Nullable>enable</Nullable>` in the csproj. Address warnings incrementally. Reserve `!` for genuinely known-safe cases with comments.

---

## Dependency Injection Lifetimes

### Scoped service injected into singleton (captive dependency)

- **Pattern to check:** A singleton service receives a scoped or transient dependency via constructor injection.
- **Why it matters:** The scoped/transient instance is captured and reused for the app's lifetime, causing stale data, thread-safety issues, and memory leaks.
- **Fix:** Inject `IServiceScopeFactory` or `IServiceProvider` into the singleton and resolve scoped services per-operation.

### Transient service holding state

- **Pattern to check:** A service registered as `Transient` that stores mutable state in fields or properties.
- **Why it matters:** Each injection creates a new instance, so state is silently lost between uses. Callers may incorrectly assume shared state.
- **Fix:** Change to `Scoped` if state is per-request, or `Singleton` if state is global. Transient services should be stateless.

### Everything registered as Singleton

- **Pattern to check:** Blanket singleton registration for services that depend on per-request data (e.g., current user, tenant context).
- **Why it matters:** Per-request context is shared across all requests, causing data leaks between users.
- **Fix:** Use Scoped for services that depend on request context. Singleton only for truly stateless or app-wide shared services.

---

## EF Core

### N+1 queries

- **Pattern to check:** Loops that access navigation properties without `.Include()`, or LINQ queries that trigger per-row subqueries.
- **Why it matters:** Generates hundreds of round-trips to the database; performance degrades linearly with row count.
- **Fix:** Use `.Include()` / `.ThenInclude()` for eager loading, or project with `.Select()` to fetch only needed columns.

### Tracking queries where not needed

- **Pattern to check:** Read-only queries without `.AsNoTracking()` or `QueryTrackingBehavior.NoTracking`.
- **Why it matters:** Tracking adds overhead for snapshot comparison and increases memory usage for read-heavy workloads.
- **Fix:** Use `.AsNoTracking()` for read-only queries. Consider setting `NoTracking` as the default and opting in to tracking when needed.

### Missing database indexes

- **Pattern to check:** Columns used in `WHERE`, `ORDER BY`, or `JOIN` clauses that have no corresponding index in the migration or schema.
- **Why it matters:** Full table scans on large tables cause query timeouts and high CPU under load.
- **Fix:** Add indexes via `HasIndex()` in EF configuration or raw SQL migration. Use `EXPLAIN ANALYZE` to verify query plans.

### Lazy loading without awareness

- **Pattern to check:** `UseLazyLoadingProxies()` enabled with no team conventions on when navigation properties are accessed.
- **Why it matters:** Silently generates queries in unexpected places (serializers, views, logging), amplifying N+1 problems.
- **Fix:** Prefer explicit loading (`.Include()`) or projection (`.Select()`). If lazy loading is used, enforce conventions with code review.

---

## FastEndpoints

### Missing request validators

- **Pattern to check:** Endpoints that accept request DTOs without a corresponding `Validator<TRequest>` class.
- **Why it matters:** Invalid data reaches business logic, causing cryptic errors or data corruption.
- **Fix:** Create a `Validator<TRequest>` using FluentValidation rules for every endpoint that accepts input.

### Missing authorization in Configure()

- **Pattern to check:** `Configure()` methods that do not call `Roles()`, `Policies()`, `Permissions()`, or `AllowAnonymous()`.
- **Why it matters:** Endpoint may be unintentionally open to unauthenticated or unauthorized users.
- **Fix:** Explicitly declare authorization requirements in `Configure()`. Use `AllowAnonymous()` only when intentional.

### Untyped or inconsistent response models

- **Pattern to check:** Endpoints that return raw `object`, anonymous types, or inconsistent shapes between success and error paths.
- **Why it matters:** API consumers cannot rely on a stable contract; generates poor OpenAPI specs.
- **Fix:** Define explicit response DTOs. Use `TypedResults` or FastEndpoints' response type configuration.

---

## SOLID Violations

### Concrete dependencies in constructors

- **Pattern to check:** Constructor parameters that are concrete classes rather than interfaces.
- **Why it matters:** Tight coupling prevents unit testing with mocks and makes swapping implementations expensive.
- **Fix:** Extract an interface and inject the abstraction. Register the concrete type in DI against the interface.

### God services (>300 lines or >7 dependencies)

- **Pattern to check:** Service classes with many constructor parameters (>7) or that exceed 300 lines.
- **Why it matters:** Indicates the class has too many responsibilities, making it hard to test, understand, and modify.
- **Fix:** Split into focused services by responsibility. Use the mediator pattern or domain events for cross-cutting coordination.

### Interface bloat (>7 methods)

- **Pattern to check:** Interfaces with many methods where most implementors only use a subset.
- **Why it matters:** Violates Interface Segregation; forces implementors to carry dead methods and makes mocking expensive.
- **Fix:** Split into smaller, role-specific interfaces. Clients should depend only on the methods they use.

---

## Exception Handling

### Swallowed exceptions (empty catch blocks)

- **Pattern to check:** `catch` blocks that are empty or contain only a comment.
- **Why it matters:** Hides failures, making bugs impossible to diagnose in production.
- **Fix:** At minimum, log the exception. If the exception is intentionally ignored, document why with a comment and log at Debug level.

### Catch-all without filtering

- **Pattern to check:** `catch (Exception ex)` that handles all exception types uniformly.
- **Why it matters:** Catches `OutOfMemoryException`, `StackOverflowException`, and other fatal errors that should not be caught.
- **Fix:** Catch specific exception types. If a catch-all is needed, re-throw fatal exceptions and log the rest.

### Missing structured logging in catch blocks

- **Pattern to check:** Catch blocks that use `Console.WriteLine` or string interpolation for error output instead of `ILogger`.
- **Why it matters:** Unstructured output is not captured by log aggregators (Serilog, Seq, Application Insights), making production debugging difficult.
- **Fix:** Use `_logger.LogError(ex, "Message with {Param}", param)` with structured logging.

---

## Build Configuration & Package Management

These files are easy to skip during an audit because they don't contain business logic — but drift and misconfigurations here cause silent build breakage, non-reproducible deploys, and supply-chain risk. Scan them deliberately.

### Wildcard / floating package versions

- **Pattern to check:** `*.csproj` or `Directory.Packages.props` entries with `Version="*"`, `Version="[1.0.0,)"`, or other open-ended version ranges.
- **Why it matters:** The build resolves to whatever version happens to be on NuGet.org on the day of the build — CI and local machines can get different binaries, and a transitive breaking change upstream can silently break the build overnight. For preview/experimental packages (e.g., `Microsoft.Extensions.AI`, `Google.GenAI`) the risk is acute because they ship breaking changes frequently.
- **Fix:** Pin to a specific version or a narrow range (`Version="1.2.3"` or `Version="[1.2.0,1.3.0)"`). If staying on latest is desired, use Dependabot/Renovate to bump versions in PRs so the change is reviewed.
- **Severity:** High — this is a reproducible-build issue, not a style issue.

### TargetFramework drift across the solution

- **Pattern to check:** `Directory.Build.props` sets one `TargetFramework` (e.g., `net8.0`) while individual csproj files override to a different framework (e.g., `net10.0`). Also check `global.json`'s SDK version vs the actual frameworks being built.
- **Why it matters:** Warnings-as-errors on one TFM may not fire on another; NuGet resolution picks different binaries per TFM. The "default" from `Directory.Build.props` is a lie if every project overrides it, and new projects added without the override will silently build on the wrong framework.
- **Fix:** Either remove the `TargetFramework` line from `Directory.Build.props` (forcing each project to declare explicitly) or update it to match reality and delete redundant overrides from csproj files. Align `global.json`'s SDK version with the highest TFM in use.

### Central Package Management without the props file

- **Pattern to check:** `Directory.Build.props` contains `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` but there is no `Directory.Packages.props` at the repo root. Or the props file exists but csproj files still carry `Version="..."` attributes on `<PackageReference>`.
- **Why it matters:** With central management enabled, NuGet expects all versions in `Directory.Packages.props`. Missing the file or mixing with per-csproj versions produces NU1507 warnings — which, combined with `TreatWarningsAsErrors=true`, break the build non-deterministically as packages are touched.
- **Fix:** Either create a complete `Directory.Packages.props` and strip versions from all `<PackageReference>` entries, or disable central management until you're ready to commit to it.

### SDK version mismatch

- **Pattern to check:** `global.json` pins an SDK version (`"version": "8.0.100"`) that doesn't match the SDK installed on CI or the TFM of the projects. Also doc comments that reference old framework versions (e.g., "uses EF Core 8.0.0" when actual is 10.0.0).
- **Why it matters:** Builds may succeed locally but fail in CI, or vice versa. Stale framework-version comments in README/Startup.cs mislead new contributors and hide the actual migration state of the codebase.
- **Fix:** Update `global.json` to the SDK range actually in use, and sync doc comments to reality. Treat stale version references as tech debt, not harmless comments.

### Unused project references or DI registrations anchored by assembly scanning

- **Pattern to check:** `<ProjectReference>` entries to projects that are never `using`-imported; services registered in `services.AddScoped<...>` but never injected anywhere. In codebases using `FastEndpoints` or MediatR assembly scanning, check whether services are only "kept alive" by `typeof(X).Assembly` references in Startup — deleting them naively breaks endpoint discovery.
- **Why it matters:** Dead services rot — they drift from endpoint contracts (e.g., an `UpdateEventAsync` on a service that takes 3 fields while the endpoint updates 9) because nobody notices. Assembly-scanning patterns hide the dependency, so "unused" static analysis tools miss it.
- **Fix:** Grep the whole repo for the service's type name. If it appears only in DI registration + tests + `typeof(X).Assembly`, either consolidate the assembly scanning reference and delete the service, or wire the service into the endpoint that's duplicating its logic.

### Mixed path separators in project files

- **Pattern to check:** `<ProjectReference Include="..\..\path\to.csproj">` (backslashes) alongside sibling entries using `<ProjectReference Include="../../path/to.csproj">` (forward slashes).
- **Why it matters:** Backslashes are Windows-only; mixed usage causes build failures on Linux/macOS runners. This is pure inconsistency — MSBuild accepts both, which is why it survives in codebases.
- **Fix:** Standardize on forward slashes (work on every platform). Add an editorconfig rule if drift keeps reappearing.

### TreatWarningsAsErrors without a clean warning baseline

- **Pattern to check:** `Directory.Build.props` or csproj with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` while the build log shows warnings being suppressed per-project via `NoWarn` entries.
- **Why it matters:** The "strict build" claim is defeated by per-project suppression lists. New warnings that would genuinely be important get added to `NoWarn` to unblock builds, gradually eroding the protection.
- **Fix:** Periodically audit `NoWarn` entries — for each suppressed warning, either fix the underlying issue or document why suppression is permanent. Target an empty `NoWarn` across the solution.

---

## Configuration and Secrets

### Secrets in appsettings.json

- **Pattern to check:** Connection strings, API keys, tokens, or passwords stored in `appsettings.json` or `appsettings.Development.json` committed to source control.
- **Why it matters:** Secrets in version control are exposed to all contributors and persist in git history even after removal.
- **Fix:** Use User Secrets (`dotnet user-secrets`) for development, environment variables or a vault (Azure Key Vault, AWS Secrets Manager) for production. Add secrets files to `.gitignore`.

### Hardcoded connection strings

- **Pattern to check:** Connection strings embedded directly in C# code as string literals.
- **Why it matters:** Cannot be changed without recompilation; credentials are visible in decompiled assemblies.
- **Fix:** Load from `IConfiguration` with the `ConnectionStrings` section. Inject `IOptions<T>` for typed configuration.

### Missing environment-specific configuration

- **Pattern to check:** Single `appsettings.json` with no `appsettings.{Environment}.json` overrides.
- **Why it matters:** Production, staging, and development share the same configuration, risking accidental use of development settings in production.
- **Fix:** Use environment-specific config files and validate required settings at startup with `IOptions<T>.Validate()`.
