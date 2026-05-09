# Issue #18 — Central Package Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move every NuGet package version out of individual `.csproj` files and into a single `Directory.Packages.props` at the repo root, enabling `ManagePackageVersionsCentrally`. After this lands, every new project (Layer 2 storage, Layer 3 runtime, Layer 4 SDK) inherits a single source of truth for package versions, eliminating drift before it can start.

**Architecture:** Pure build-config refactor. No code changes, no behaviour changes, no test additions. Existing `.csproj` files keep their `<PackageReference Include="...">` entries but lose the `Version="..."` attribute; versions move to `<PackageVersion>` entries in the new `Directory.Packages.props`.

**Tech Stack:** MSBuild, NuGet Central Package Management (CPM).

**Branch:** `feat/central-package-management` (to be created)

**Issue:** [#18](https://github.com/satish-krishna/sigil/issues/18)

**Note on stale issue body:** The issue (filed before PR #19) lists `FluentAssertions` as a candidate for the central table and notes it's "pinned to 6.x for license reasons". PR #19 replaced FluentAssertions with **Shouldly 4.3.0** as the project standard. The plan reflects current reality, not the issue body.

---

## File Structure

### Created

```
Directory.Packages.props          # repo root — single source of truth for all NuGet versions
```

### Modified

```
src/Sigil.Core/Sigil.Core.csproj
src/Sigil.Api/Sigil.Api.csproj
src/Sigil.Storage.Mongo/Sigil.Storage.Mongo.csproj
src/Sigil.Storage.EfCore/Sigil.Storage.EfCore.csproj
tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj
```

`Sigil.Agent.SDK.csproj`, `Sigil.Infrastructure.csproj`, and `Sigil.Runtime.csproj` carry no `<PackageReference>` entries today and need no edits.

---

## Current package surface (snapshot — verify with grep before editing)

| Package | Version | Project |
|---|---|---|
| `CSharpFunctionalExtensions` | 3.7.0 | Sigil.Core |
| `FastEndpoints` | 8.1.0 | Sigil.Api |
| `FastEndpoints.Swagger` | 8.1.0 | Sigil.Api |
| `MongoDB.Driver` | 3.8.0 | Sigil.Storage.Mongo |
| `Microsoft.EntityFrameworkCore` | 9.0.15 | Sigil.Storage.EfCore |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.15 | Sigil.Storage.EfCore |
| `Microsoft.NET.Test.Sdk` | 18.5.1 | Sigil.Core.Tests |
| `xunit` | 2.9.3 | Sigil.Core.Tests |
| `xunit.runner.visualstudio` | 3.1.5 | Sigil.Core.Tests |
| `Shouldly` | 4.3.0 | Sigil.Core.Tests |

10 distinct `<PackageVersion>` entries.

---

## Tasks

- [ ] **1. Verify current package surface.**
  Run `Grep PackageReference --glob "*.csproj"` and confirm the 10 entries above are still accurate. Update the table if anything has changed since this plan was written.

- [ ] **2. Create `Directory.Packages.props` at repo root.**
  Sort entries alphabetically by package id. Group test-only packages under a comment for clarity. Set `ManagePackageVersionsCentrally=true` and (defensive) `CentralPackageTransitivePinningEnabled=true` to prevent transitive overrides surprising future contributors.

  ```xml
  <Project>
    <PropertyGroup>
      <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
      <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    </PropertyGroup>

    <ItemGroup>
      <PackageVersion Include="CSharpFunctionalExtensions" Version="3.7.0" />
      <PackageVersion Include="FastEndpoints" Version="8.1.0" />
      <PackageVersion Include="FastEndpoints.Swagger" Version="8.1.0" />
      <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.15" />
      <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.15" />
      <PackageVersion Include="MongoDB.Driver" Version="3.8.0" />
    </ItemGroup>

    <!-- Test-only packages -->
    <ItemGroup>
      <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
      <PackageVersion Include="Shouldly" Version="4.3.0" />
      <PackageVersion Include="xunit" Version="2.9.3" />
      <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    </ItemGroup>
  </Project>
  ```

- [ ] **3. Strip `Version` from every `<PackageReference>` in the 5 modified csprojs.**
  Use `Edit` per file. Preserve any nested elements (e.g. `<PrivateAssets>`, `<IncludeAssets>` on `xunit.runner.visualstudio` and `Microsoft.EntityFrameworkCore.Design`) — only the `Version="..."` attribute is removed. Do not touch `<ProjectReference>` entries.

- [ ] **4. Build verification.**
  `dotnet build sigil.sln` must produce 0 warnings and 0 errors. Watch specifically for NuGet warning **NU1604** (project dependency without version) and **NU1008** (CPM enabled but version on PackageReference) — both indicate a botched migration.

- [ ] **5. Test verification.**
  `dotnet test sigil.sln` must report 59/59 passing (the count after PR #19).

- [ ] **6. Restore-from-clean check.**
  Delete `bin/`, `obj/`, and any `~/.nuget/packages/.staging` left over, then `dotnet restore sigil.sln && dotnet build sigil.sln` to confirm a fresh restore picks up versions purely from `Directory.Packages.props` and not from a stale lock or cache.

- [ ] **7. Search for forgotten Version attributes.**
  `Grep -E 'PackageReference[^>]*Version=' --glob "*.csproj"` must return zero matches.

---

## Acceptance (mirrors issue #18)

- `Directory.Packages.props` exists at repo root.
- No `<PackageReference>` in any `.csproj` carries a `Version` attribute.
- `dotnet build sigil.sln` is clean (0 warnings, 0 errors).
- `dotnet test sigil.sln` is green (59/59).

---

## Out of scope

- Package upgrades. This PR keeps every version exactly where it is today; bumps belong in separate, reviewable PRs.
- Adding new packages for Layer 2+ work (Mongo Testcontainers, Polly, JWT bearer, etc.). Those land alongside the issues that need them — but they will land into the central table from day one because CPM is now enforced.
- `global.json` SDK pinning, `nuget.config` source changes, or `Directory.Build.targets`. None are required for CPM and none are touched here.

---

## Risk & rollback

CPM is a build-system feature with well-trodden migration paths. The migration is mechanical and additive — every package keeps its existing version. If `dotnet restore` fails after the change, rollback is `git revert` of the single commit; nothing on disk gets corrupted.

The one realistic failure mode is `NU1008` if a `Version` attribute is missed during the strip pass — task 7 catches this explicitly.
