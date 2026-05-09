# Issue #4 — Sigil-Key Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land Open-tier Sigil-Key validation in the Sigil kernel: contracts in `Sigil.Core/Security/`, a `SigilKeyValidator` in `Sigil.Infrastructure/Security/`, and tests proving the registration flow rejects missing or invalid keys. The HTTP endpoint that consumes this lives in Issue #13 and is out of scope.

**Architecture:** Single `ISigilSecurity` facade (Phase 1 impl is `SigilKeyValidator`). Static allowlist keyed by `AgentId` lives in kernel config (`Security:OpenTier:Keys:<agentId>: <key>`). Credentials travel as `X-Sigil-Key` header, are wrapped in a generic `SigilCredentials` bag, and validated against the allowlist in constant time. After success the kernel persists `SecurityProfile.Tier = Open` on the registration; the key itself is never persisted.

**Tech Stack:** .NET 9, xUnit + Shouldly, `CSharpFunctionalExtensions.Result`, `Microsoft.Extensions.Options` / `.Configuration` / `.DependencyInjection` / `.Logging.Abstractions` (all 9.0.x). Central package management via `Directory.Packages.props`.

**Spec:** [`.bob/plans/2026-05-09-issue-04-sigil-key-validation.md`](./2026-05-09-issue-04-sigil-key-validation.md)

---

## Pre-flight notes (read before starting)

- **Working directory:** `D:\Repos\sigil`. All paths are relative to repo root.
- **Build/test commands:** `dotnet build sigil.sln` and `dotnet test sigil.sln`. `TreatWarningsAsErrors=true` is on globally — nullability warnings, unused usings, and analyzer warnings will fail the build.
- **Test framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`) with Shouldly (`ShouldBe`, `ShouldBeNull`, `ShouldBeEmpty`, etc.). Don't use FluentAssertions — Sigil standardizes on Shouldly.
- **Result type:** `CSharpFunctionalExtensions.Result<T>`. Construct via `Result.Success(value)` / `Result.Failure<T>("error-code")`. Inspect via `result.IsSuccess`, `result.IsFailure`, `result.Value`, `result.Error`.
- **AgentId:** `Sigil.Core.Identity.AgentId` is a `readonly record struct` with a single `string Value` field (see `src/Sigil.Core/Identity/AgentId.cs`). Construct with `new AgentId("agent-name")`. JSON serializes as a string via `AgentIdJsonConverter`.
- **Implicit usings + nullable:** enabled solution-wide. You still need explicit `using` for `System.Text.Json`, `System.Text.Json.Serialization`, `CSharpFunctionalExtensions`, `Microsoft.Extensions.*`.
- **Hooks:** `PostToolUse` runs Prettier on edited `.cs` files (per `.claude/settings.json`). If a Prettier reformat changes whitespace after a Write/Edit, accept it and proceed — no separate action needed.
- **Commit cadence:** one commit per task. Conventional-commit prefixes: `feat(core)`, `feat(infra)`, `chore(build)`, `test(core)`, `test(infra)`. Match recent log style: `feat(core): land storage contracts and identity types (#2) (#17)`.
- **Shared test helpers (used by Tasks 10–15):** A small `TestOptionsMonitor<T>` is introduced in Task 10; later tasks reuse it. The exact code appears in Task 10 — don't reinvent it.

### File structure summary

| File | Responsibility | Created in |
|---|---|---|
| `src/Sigil.Core/Security/SecurityTier.cs` | Enum `SecurityTier { Open, Standard, Trusted }` with string JSON converter | Task 1 |
| `src/Sigil.Core/Security/SigilCredentials.cs` | Generic transport bag for tier-agnostic credentials | Task 2 |
| `src/Sigil.Core/Security/AuthenticationResult.cs` | Successful auth outcome (`AgentId`, `Tier`) | Task 3 |
| `src/Sigil.Core/Security/ISigilSecurity.cs` | The single security facade interface | Task 4 |
| `src/Sigil.Core/Security/SigilSecurityErrors.cs` | Public string constants for failure reasons | Task 4 |
| `src/Sigil.Core/Registry/SecurityProfile.cs` | Patched: add `Tier` field + doc-comment on `SigilKey` | Task 5 |
| `Directory.Packages.props` | Central versions for new ME.* packages | Task 6 |
| `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj` | Add new package references | Task 7 |
| `tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj` | New test project | Task 8 |
| `src/Sigil.Infrastructure/Security/SigilSecurityOptions.cs` | `IOptions`-bound config record | Task 9 |
| `src/Sigil.Infrastructure/Security/SigilKeyValidator.cs` | Phase 1 `ISigilSecurity` impl | Tasks 10–13 |
| `src/Sigil.Infrastructure/Security/ServiceCollectionExtensions.cs` | `AddSigilSecurity(IConfiguration)` DI extension | Task 14 |

Test files mirror this layout under `tests/` and are introduced alongside the code that exercises them.

---

## Task 1: `SecurityTier` enum

**Files:**
- Create: `src/Sigil.Core/Security/SecurityTier.cs`
- Test: `tests/Sigil.Core.Tests/Security/SecurityTierTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Core.Tests/Security/SecurityTierTests.cs`:

```csharp
using System.Text.Json;
using Shouldly;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SecurityTierTests
{
    [Fact]
    public void Default_Value_Is_Open()
    {
        default(SecurityTier).ShouldBe(SecurityTier.Open);
    }

    [Theory]
    [InlineData(SecurityTier.Open, "\"Open\"")]
    [InlineData(SecurityTier.Standard, "\"Standard\"")]
    [InlineData(SecurityTier.Trusted, "\"Trusted\"")]
    public void Serializes_As_String(SecurityTier value, string expectedJson)
    {
        JsonSerializer.Serialize(value).ShouldBe(expectedJson);
    }

    [Theory]
    [InlineData("\"Open\"", SecurityTier.Open)]
    [InlineData("\"Standard\"", SecurityTier.Standard)]
    [InlineData("\"Trusted\"", SecurityTier.Trusted)]
    public void Deserializes_From_String(string json, SecurityTier expected)
    {
        JsonSerializer.Deserialize<SecurityTier>(json).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SecurityTierTests"
```

Expected: build error — `SecurityTier` does not exist in namespace `Sigil.Core.Security`.

- [ ] **Step 3: Write the enum**

Create `src/Sigil.Core/Security/SecurityTier.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Sigil.Core.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityTier
{
    Open,
    Standard,
    Trusted
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SecurityTierTests"
```

Expected: 7 tests pass (1 default + 3 serialize + 3 deserialize).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Security/SecurityTier.cs tests/Sigil.Core.Tests/Security/SecurityTierTests.cs
git commit -m "feat(core): add SecurityTier enum"
```

---

## Task 2: `SigilCredentials` record

**Files:**
- Create: `src/Sigil.Core/Security/SigilCredentials.cs`
- Test: `tests/Sigil.Core.Tests/Security/SigilCredentialsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Core.Tests/Security/SigilCredentialsTests.cs`:

```csharp
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SigilCredentialsTests
{
    [Fact]
    public void Defaults_HaveNullOptionalFields()
    {
        var c = new SigilCredentials { AgentId = new AgentId("agent-1") };

        c.AgentId.ShouldBe(new AgentId("agent-1"));
        c.SigilKey.ShouldBeNull();
        c.Jwt.ShouldBeNull();
        c.CertificateThumbprint.ShouldBeNull();
    }

    [Fact]
    public void TwoCredentials_FromIndependentConstruction_AreEqual()
    {
        var a = new SigilCredentials
        {
            AgentId = new AgentId("a"),
            SigilKey = "k",
            Jwt = "j",
            CertificateThumbprint = "t"
        };
        var b = new SigilCredentials
        {
            AgentId = new AgentId("a"),
            SigilKey = "k",
            Jwt = "j",
            CertificateThumbprint = "t"
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoCredentials_DifferingInSigilKey_AreNotEqual()
    {
        var a = new SigilCredentials { AgentId = new AgentId("a"), SigilKey = "k1" };
        var b = new SigilCredentials { AgentId = new AgentId("a"), SigilKey = "k2" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void RoundTrip_Json_PreservesAllFields()
    {
        var c = new SigilCredentials
        {
            AgentId = new AgentId("agent-1"),
            SigilKey = "secret",
            Jwt = null,
            CertificateThumbprint = null
        };

        var json = JsonSerializer.Serialize(c);
        var back = JsonSerializer.Deserialize<SigilCredentials>(json);

        back.ShouldBe(c);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilCredentialsTests"
```

Expected: build error — `SigilCredentials` does not exist.

- [ ] **Step 3: Write the record**

Create `src/Sigil.Core/Security/SigilCredentials.cs`:

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Security;

public sealed record SigilCredentials
{
    public required AgentId AgentId { get; init; }
    public string? SigilKey { get; init; }
    public string? Jwt { get; init; }
    public string? CertificateThumbprint { get; init; }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilCredentialsTests"
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Security/SigilCredentials.cs tests/Sigil.Core.Tests/Security/SigilCredentialsTests.cs
git commit -m "feat(core): add SigilCredentials transport DTO"
```

---

## Task 3: `AuthenticationResult` record

**Files:**
- Create: `src/Sigil.Core/Security/AuthenticationResult.cs`
- Test: `tests/Sigil.Core.Tests/Security/AuthenticationResultTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Core.Tests/Security/AuthenticationResultTests.cs`:

```csharp
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class AuthenticationResultTests
{
    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };
        var b = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoResults_DifferingInTier_AreNotEqual()
    {
        var a = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };
        var b = a with { Tier = SecurityTier.Standard };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void RoundTrip_Json_PreservesValues()
    {
        var ar = new AuthenticationResult
        {
            AgentId = new AgentId("agent-1"),
            Tier = SecurityTier.Trusted
        };

        var json = JsonSerializer.Serialize(ar);
        var back = JsonSerializer.Deserialize<AuthenticationResult>(json);

        back.ShouldBe(ar);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AuthenticationResultTests"
```

Expected: build error — `AuthenticationResult` does not exist.

- [ ] **Step 3: Write the record**

Create `src/Sigil.Core/Security/AuthenticationResult.cs`:

```csharp
using Sigil.Core.Identity;

namespace Sigil.Core.Security;

public sealed record AuthenticationResult
{
    public required AgentId AgentId { get; init; }
    public required SecurityTier Tier { get; init; }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~AuthenticationResultTests"
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Security/AuthenticationResult.cs tests/Sigil.Core.Tests/Security/AuthenticationResultTests.cs
git commit -m "feat(core): add AuthenticationResult"
```

---

## Task 4: `ISigilSecurity` interface + `SigilSecurityErrors` constants

**Files:**
- Create: `src/Sigil.Core/Security/ISigilSecurity.cs`
- Create: `src/Sigil.Core/Security/SigilSecurityErrors.cs`
- Test: `tests/Sigil.Core.Tests/Security/SigilSecurityErrorsTests.cs`

The interface itself has no behavior to test directly; it's exercised via the validator in later tasks. We pin the error-code strings with a small test so typos fail loudly at the boundary callers will pattern-match against.

- [ ] **Step 1: Write the failing test**

Create `tests/Sigil.Core.Tests/Security/SigilSecurityErrorsTests.cs`:

```csharp
using Shouldly;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SigilSecurityErrorsTests
{
    [Fact]
    public void ErrorCodes_HaveStableValues()
    {
        SigilSecurityErrors.MissingKey.ShouldBe("missing-key");
        SigilSecurityErrors.UnknownAgent.ShouldBe("unknown-agent");
        SigilSecurityErrors.KeyMismatch.ShouldBe("key-mismatch");
        SigilSecurityErrors.TierNotSupported.ShouldBe("tier-not-supported");
        SigilSecurityErrors.ModeMismatch.ShouldBe("mode-mismatch");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilSecurityErrorsTests"
```

Expected: build error — `SigilSecurityErrors` does not exist.

- [ ] **Step 3: Write the constants and the interface**

Create `src/Sigil.Core/Security/SigilSecurityErrors.cs`:

```csharp
namespace Sigil.Core.Security;

public static class SigilSecurityErrors
{
    public const string MissingKey = "missing-key";
    public const string UnknownAgent = "unknown-agent";
    public const string KeyMismatch = "key-mismatch";
    public const string TierNotSupported = "tier-not-supported";
    public const string ModeMismatch = "mode-mismatch";
}
```

Create `src/Sigil.Core/Security/ISigilSecurity.cs`:

```csharp
using CSharpFunctionalExtensions;

namespace Sigil.Core.Security;

public interface ISigilSecurity
{
    Task<Result<AuthenticationResult>> AuthenticateAsync(
        SigilCredentials credentials,
        SecurityTier requiredTier,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Run test to verify it passes and the solution still builds**

```bash
dotnet build sigil.sln
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SigilSecurityErrorsTests"
```

Expected: build succeeds; 1 test passes.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Security/ISigilSecurity.cs src/Sigil.Core/Security/SigilSecurityErrors.cs tests/Sigil.Core.Tests/Security/SigilSecurityErrorsTests.cs
git commit -m "feat(core): add ISigilSecurity facade and error codes"
```

---

## Task 5: Extend `SecurityProfile` with `Tier`

**Files:**
- Modify: `src/Sigil.Core/Registry/SecurityProfile.cs`
- Modify: `tests/Sigil.Core.Tests/Registry/SecurityProfileTests.cs`

The existing `SecurityProfile.cs` has hand-rolled `Equals`/`GetHashCode` because `IReadOnlyList<string> AllowedTools` would otherwise use reference equality. Adding `Tier` requires updating both methods.

- [ ] **Step 1: Write the failing tests**

Append three new tests to `tests/Sigil.Core.Tests/Registry/SecurityProfileTests.cs`. The full updated file:

```csharp
using Shouldly;
using Sigil.Core.Registry;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class SecurityProfileTests
{
    [Fact]
    public void Defaults_HaveEmptyAllowedToolsAndNoSecrets()
    {
        var s = new SecurityProfile();

        s.CertificateThumbprint.ShouldBeNull();
        s.SigilKey.ShouldBeNull();
        s.IsPiiCleared.ShouldBeFalse();
        s.AllowedTools.ShouldBeEmpty();
    }

    [Fact]
    public void Default_Tier_Is_Open()
    {
        new SecurityProfile().Tier.ShouldBe(SecurityTier.Open);
    }

    [Fact]
    public void TwoProfiles_FromIndependentConstruction_AreEqual()
    {
        var a = new SecurityProfile
        {
            CertificateThumbprint = "abc",
            SigilKey = "key",
            IsPiiCleared = true,
            AllowedTools = new[] { "tool1", "tool2" },
            Tier = SecurityTier.Trusted
        };
        var b = new SecurityProfile
        {
            CertificateThumbprint = "abc",
            SigilKey = "key",
            IsPiiCleared = true,
            AllowedTools = new[] { "tool1", "tool2" },
            Tier = SecurityTier.Trusted
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoProfiles_DifferingInAllowedTools_AreNotEqual()
    {
        var a = new SecurityProfile { AllowedTools = new[] { "tool1" } };
        var b = new SecurityProfile { AllowedTools = new[] { "tool2" } };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoProfiles_DifferingOnlyInTier_AreNotEqual()
    {
        var a = new SecurityProfile { Tier = SecurityTier.Open };
        var b = new SecurityProfile { Tier = SecurityTier.Standard };

        a.ShouldNotBe(b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj --filter "FullyQualifiedName~SecurityProfileTests"
```

Expected: build error — `Tier` is not a member of `SecurityProfile`.

- [ ] **Step 3: Patch `SecurityProfile.cs`**

Replace `src/Sigil.Core/Registry/SecurityProfile.cs` with:

```csharp
using Sigil.Core.Security;

namespace Sigil.Core.Registry;

public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }

    /// <summary>
    /// Pre-shared key presented at registration. Not persisted by the kernel at Open tier;
    /// the kernel-configured allowlist is the source of truth. Future tiers may persist a
    /// hash or token-derived credential here.
    /// </summary>
    public string? SigilKey { get; init; }

    public bool IsPiiCleared { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public SecurityTier Tier { get; init; } = SecurityTier.Open;

    public bool Equals(SecurityProfile? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CertificateThumbprint == other.CertificateThumbprint
            && SigilKey == other.SigilKey
            && IsPiiCleared == other.IsPiiCleared
            && AllowedTools.SequenceEqual(other.AllowedTools)
            && Tier == other.Tier;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CertificateThumbprint);
        hash.Add(SigilKey);
        hash.Add(IsPiiCleared);
        foreach (var tool in AllowedTools) hash.Add(tool);
        hash.Add(Tier);
        return hash.ToHashCode();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass and full suite is green**

```bash
dotnet test tests/Sigil.Core.Tests/Sigil.Core.Tests.csproj
```

Expected: all tests in `Sigil.Core.Tests` pass — including the existing `AgentRegistrationTests` (the `Tier` field defaulting to `Open` keeps round-trip equality intact).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Core/Registry/SecurityProfile.cs tests/Sigil.Core.Tests/Registry/SecurityProfileTests.cs
git commit -m "feat(core): add Tier to SecurityProfile"
```

---

## Task 6: Pin new package versions in `Directory.Packages.props`

**Files:**
- Modify: `Directory.Packages.props`

`Sigil.Infrastructure` will need ME.Options binding, ME.Logging, ME.DI Abstractions. The new test project will need the concrete `ME.Configuration.Memory` + `ME.DependencyInjection`. Pin all at 9.0.0 (matches the .NET 9 runtime release line).

- [ ] **Step 1: Replace `Directory.Packages.props`**

Replace the file with:

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

  <!-- Microsoft.Extensions.* -->
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Memory" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="9.0.0" />
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

- [ ] **Step 2: Verify the solution still restores cleanly**

```bash
dotnet restore sigil.sln
dotnet build sigil.sln
```

Expected: restore + build succeed with no warnings (no projects reference these packages yet, so it's a no-op for the build graph).

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore(build): pin Microsoft.Extensions.* package versions"
```

---

## Task 7: Add package references to `Sigil.Infrastructure.csproj`

**Files:**
- Modify: `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj`

- [ ] **Step 1: Replace the csproj**

Replace `src/Sigil.Infrastructure/Sigil.Infrastructure.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Sigil.Core\Sigil.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify the project still builds**

```bash
dotnet build src/Sigil.Infrastructure/Sigil.Infrastructure.csproj
```

Expected: build succeeds (project still has no source files beyond auto-generated ones).

- [ ] **Step 3: Commit**

```bash
git add src/Sigil.Infrastructure/Sigil.Infrastructure.csproj
git commit -m "chore(build): wire Sigil.Infrastructure to Microsoft.Extensions.*"
```

---

## Task 8: Create `Sigil.Infrastructure.Tests` project

**Files:**
- Create: `tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj`
- Modify: `sigil.sln` (add the new project)

- [ ] **Step 1: Create the csproj**

Create `tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.Options" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Sigil.Infrastructure\Sigil.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution**

```bash
dotnet sln sigil.sln add tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --solution-folder tests
```

Expected: success message; `sigil.sln` is updated with a new project entry under the `tests` solution folder.

- [ ] **Step 3: Verify the solution builds**

```bash
dotnet build sigil.sln
```

Expected: clean build. The new test project has no source files yet — that's fine; xUnit + test SDK can produce an empty test assembly.

- [ ] **Step 4: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj sigil.sln
git commit -m "test(infra): scaffold Sigil.Infrastructure.Tests project"
```

---

## Task 9: `SigilSecurityOptions`

**Files:**
- Create: `src/Sigil.Infrastructure/Security/SigilSecurityOptions.cs`
- Test: `tests/Sigil.Infrastructure.Tests/Security/SigilSecurityOptionsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Security/SigilSecurityOptionsTests.cs`:

```csharp
using Shouldly;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class SigilSecurityOptionsTests
{
    [Fact]
    public void Defaults_AreOpenModeAndEmptyAllowlist()
    {
        var opts = new SigilSecurityOptions();

        opts.Mode.ShouldBe(SecurityTier.Open);
        opts.OpenTier.ShouldNotBeNull();
        opts.OpenTier.Keys.ShouldBeEmpty();
    }

    [Fact]
    public void OpenTier_KeyComparison_IsOrdinalAndCaseSensitive()
    {
        var opts = new SigilSecurityOptions();
        opts.OpenTier.Keys["Echo-Agent"] = "k";

        opts.OpenTier.Keys.ContainsKey("echo-agent").ShouldBeFalse();
        opts.OpenTier.Keys.ContainsKey("Echo-Agent").ShouldBeTrue();
    }

    [Fact]
    public void SectionName_Is_Security()
    {
        SigilSecurityOptions.SectionName.ShouldBe("Security");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj
```

Expected: build error — `SigilSecurityOptions` does not exist.

- [ ] **Step 3: Create the options class**

Create `src/Sigil.Infrastructure/Security/SigilSecurityOptions.cs`:

```csharp
using Sigil.Core.Security;

namespace Sigil.Infrastructure.Security;

public sealed class SigilSecurityOptions
{
    public const string SectionName = "Security";

    public SecurityTier Mode { get; set; } = SecurityTier.Open;

    public OpenTierOptions OpenTier { get; set; } = new();

    public sealed class OpenTierOptions
    {
        public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Security/SigilSecurityOptions.cs tests/Sigil.Infrastructure.Tests/Security/SigilSecurityOptionsTests.cs
git commit -m "feat(infra): add SigilSecurityOptions"
```

---

## Task 10: `SigilKeyValidator` — happy path + test infrastructure

**Files:**
- Create: `src/Sigil.Infrastructure/Security/SigilKeyValidator.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Security/TestOptionsMonitor.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs`

This task introduces the validator class plus a small `IOptionsMonitor<T>` test double that later tasks (and Task 15) will reuse. The first behavioral test covers the success path: configured key matches presented key.

- [ ] **Step 1: Create the test double**

Create `tests/Sigil.Infrastructure.Tests/Security/TestOptionsMonitor.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace Sigil.Infrastructure.Tests.Security;

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    public TestOptionsMonitor(T initialValue)
    {
        CurrentValue = initialValue;
    }

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class SigilKeyValidatorTests
{
    private static SigilKeyValidator MakeValidator(SigilSecurityOptions options)
        => new(new TestOptionsMonitor<SigilSecurityOptions>(options),
               NullLogger<SigilKeyValidator>.Instance);

    private static SigilSecurityOptions OpenWithKey(string agentId, string key)
    {
        var o = new SigilSecurityOptions { Mode = SecurityTier.Open };
        o.OpenTier.Keys[agentId] = key;
        return o;
    }

    [Fact]
    public async Task CorrectKey_ReturnsSuccess_WithEchoedAgentIdAndOpenTier()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
        result.Value.Tier.ShouldBe(SecurityTier.Open);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: build error — `SigilKeyValidator` does not exist.

- [ ] **Step 4: Create the validator with the success path**

Create `src/Sigil.Infrastructure/Security/SigilKeyValidator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Core.Security;

namespace Sigil.Infrastructure.Security;

public sealed class SigilKeyValidator : ISigilSecurity
{
    private readonly IOptionsMonitor<SigilSecurityOptions> _options;
    private readonly ILogger<SigilKeyValidator> _logger;

    public SigilKeyValidator(
        IOptionsMonitor<SigilSecurityOptions> options,
        ILogger<SigilKeyValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<Result<AuthenticationResult>> AuthenticateAsync(
        SigilCredentials credentials,
        SecurityTier requiredTier,
        CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;

        var key = string.IsNullOrWhiteSpace(credentials.SigilKey) ? null : credentials.SigilKey;
        if (key is null)
            return Fail(SigilSecurityErrors.MissingKey);

        if (!opts.OpenTier.Keys.TryGetValue(credentials.AgentId.Value, out var expected))
            return Fail(SigilSecurityErrors.UnknownAgent);

        if (!FixedTimeKeyEquals(key, expected))
            return Fail(SigilSecurityErrors.KeyMismatch);

        _logger.LogInformation("Open-tier auth OK for {AgentId}", credentials.AgentId);
        return Task.FromResult(Result.Success(new AuthenticationResult
        {
            AgentId = credentials.AgentId,
            Tier = SecurityTier.Open
        }));
    }

    private static Task<Result<AuthenticationResult>> Fail(string code)
        => Task.FromResult(Result.Failure<AuthenticationResult>(code));

    private static bool FixedTimeKeyEquals(string presented, string expected)
    {
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        if (presentedBytes.Length == expectedBytes.Length)
            return CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes);

        // Lengths differ: still run a constant-time comparison against a same-length
        // sentinel buffer so timing doesn't leak which side was longer. Result is
        // discarded; caller treats this as a mismatch.
        var longer = presentedBytes.Length > expectedBytes.Length ? presentedBytes : expectedBytes;
        var sentinel = new byte[longer.Length];
        Buffer.BlockCopy(longer, 0, sentinel, 0, longer.Length);
        _ = CryptographicOperations.FixedTimeEquals(longer, sentinel);
        return false;
    }
}
```

> Note: Mode-mismatch and tier-not-supported guards are added in Task 12; missing-key short-circuit is already in place to keep the happy-path test deterministic against an Open-mode allowlist.

- [ ] **Step 5: Run test to verify it passes**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add src/Sigil.Infrastructure/Security/SigilKeyValidator.cs tests/Sigil.Infrastructure.Tests/Security/TestOptionsMonitor.cs tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs
git commit -m "feat(infra): add SigilKeyValidator with happy-path Open-tier auth"
```

---

## Task 11: `SigilKeyValidator` — credential failure cases

**Files:**
- Modify: `tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs`

The validator already handles missing-key, unknown-agent, and key-mismatch (added pre-emptively in Task 10 to make the happy path testable). This task adds tests asserting each failure path explicitly.

- [ ] **Step 1: Append the failing tests**

Add the following methods to `SigilKeyValidatorTests` (above the closing `}`):

```csharp
    [Fact]
    public async Task MissingKey_Returns_MissingKey()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials { AgentId = new AgentId("echo-agent"), SigilKey = null };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.MissingKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task WhitespaceKey_Returns_MissingKey(string presented)
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials { AgentId = new AgentId("echo-agent"), SigilKey = presented };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.MissingKey);
    }

    [Fact]
    public async Task UnknownAgent_Returns_UnknownAgent()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("research-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.UnknownAgent);
    }

    [Fact]
    public async Task WrongKey_Returns_KeyMismatch()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "WRONG"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.KeyMismatch);
    }
```

- [ ] **Step 2: Run tests to verify they pass**

The validator already has logic for these branches, so tests should pass on first run.

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: 6 tests pass total (1 happy path + 5 new — `MissingKey`, 3 whitespace theory cases, `UnknownAgent`, `WrongKey`).

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs
git commit -m "test(infra): cover SigilKeyValidator credential failure paths"
```

---

## Task 12: `SigilKeyValidator` — mode and tier guards

**Files:**
- Modify: `src/Sigil.Infrastructure/Security/SigilKeyValidator.cs`
- Modify: `tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs`

Add the two top-of-method guards: refuse if the kernel is configured in a non-Open mode, and refuse if the caller is asking for a tier this validator can't satisfy.

- [ ] **Step 1: Append the failing tests**

Add to `SigilKeyValidatorTests`:

```csharp
    [Fact]
    public async Task ModeMisconfigured_Returns_ModeMismatch()
    {
        var opts = new SigilSecurityOptions { Mode = SecurityTier.Standard };
        opts.OpenTier.Keys["echo-agent"] = "dev-key-echo";
        var validator = MakeValidator(opts);
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.ModeMismatch);
    }

    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task TierEscalationAboveOpen_Returns_TierNotSupported(SecurityTier requiredTier)
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, requiredTier);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.TierNotSupported);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: the new `ModeMisconfigured` test fails (returns success today). The `TierEscalation` theory fails (returns success today). The other 6 tests still pass.

- [ ] **Step 3: Add the guards to the validator**

Replace the body of `AuthenticateAsync` in `src/Sigil.Infrastructure/Security/SigilKeyValidator.cs` (everything between the method signature and the closing brace of the method) with:

```csharp
        var opts = _options.CurrentValue;

        if (opts.Mode != SecurityTier.Open)
            return Fail(SigilSecurityErrors.ModeMismatch);

        if (requiredTier != SecurityTier.Open)
            return Fail(SigilSecurityErrors.TierNotSupported);

        var key = string.IsNullOrWhiteSpace(credentials.SigilKey) ? null : credentials.SigilKey;
        if (key is null)
            return Fail(SigilSecurityErrors.MissingKey);

        if (!opts.OpenTier.Keys.TryGetValue(credentials.AgentId.Value, out var expected))
            return Fail(SigilSecurityErrors.UnknownAgent);

        if (!FixedTimeKeyEquals(key, expected))
            return Fail(SigilSecurityErrors.KeyMismatch);

        _logger.LogInformation("Open-tier auth OK for {AgentId}", credentials.AgentId);
        return Task.FromResult(Result.Success(new AuthenticationResult
        {
            AgentId = credentials.AgentId,
            Tier = SecurityTier.Open
        }));
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: 9 tests pass total (6 prior + `ModeMisconfigured` + 2 from the `TierEscalation` theory).

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Security/SigilKeyValidator.cs tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs
git commit -m "feat(infra): add mode and tier guards to SigilKeyValidator"
```

---

## Task 13: `SigilKeyValidator` — length-mismatch handling

**Files:**
- Modify: `tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs`

The validator's `FixedTimeKeyEquals` already handles unequal lengths via the same-length-sentinel pattern (added in Task 10). This task asserts the behavior with explicit cases. Both should already pass.

- [ ] **Step 1: Append the tests**

Add to `SigilKeyValidatorTests`:

```csharp
    [Theory]
    [InlineData("dev-key-echoX")]   // one byte longer
    [InlineData("dev-key-ech")]      // one byte shorter
    [InlineData("X")]                // very short
    [InlineData("dev-key-echo-very-long-suffix")] // much longer
    public async Task KeyComparisonHandlesLengthDifferences(string presented)
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = presented
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.KeyMismatch);
    }
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SigilKeyValidatorTests"
```

Expected: 13 tests pass total (9 prior + 4 new theory cases). No `ArgumentException` from `FixedTimeEquals` length-check leaking through.

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests.cs
git commit -m "test(infra): assert SigilKeyValidator handles unequal key lengths"
```

---

## Task 14: `AddSigilSecurity` DI extension + binding test

**Files:**
- Create: `src/Sigil.Infrastructure/Security/ServiceCollectionExtensions.cs`
- Create: `tests/Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class AddSigilSecurityTests
{
    private static IConfiguration BuildConfig(IDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // The test project doesn't reference Microsoft.Extensions.Logging (the package that
    // hosts AddLogging()), so register the open-generic ILogger<> -> NullLogger<>
    // directly. ILogger<T> and NullLogger<T> both come transitively via
    // Microsoft.Extensions.Logging.Abstractions.
    private static IServiceCollection NewServices()
        => new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Resolves_ISigilSecurity_As_SigilKeyValidator()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<ISigilSecurity>();

        resolved.ShouldBeOfType<SigilKeyValidator>();
    }

    [Fact]
    public void Options_Bind_From_Configuration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo",
            ["Security:OpenTier:Keys:research-agent"] = "dev-key-research"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptionsMonitor<SigilSecurityOptions>>().CurrentValue;

        opts.Mode.ShouldBe(SecurityTier.Open);
        opts.OpenTier.Keys["echo-agent"].ShouldBe("dev-key-echo");
        opts.OpenTier.Keys["research-agent"].ShouldBe("dev-key-research");
    }

    [Fact]
    public async Task Validator_Authenticates_Against_Bound_Allowlist()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var sec = provider.GetRequiredService<ISigilSecurity>();
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await sec.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AddSigilSecurityTests"
```

Expected: build error — `AddSigilSecurity` extension does not exist.

- [ ] **Step 3: Create the DI extension**

Create `src/Sigil.Infrastructure/Security/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Core.Security;

namespace Sigil.Infrastructure.Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSigilSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SigilSecurityOptions>()
            .Bind(configuration.GetSection(SigilSecurityOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ISigilSecurity, SigilKeyValidator>();
        return services;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AddSigilSecurityTests"
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Sigil.Infrastructure/Security/ServiceCollectionExtensions.cs tests/Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests.cs
git commit -m "feat(infra): add AddSigilSecurity DI extension"
```

---

## Task 15: `AddSigilSecurity` — empty allowlist + IOptionsMonitor reload

**Files:**
- Modify: `tests/Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests.cs`

Two more behaviors the spec explicitly calls out:
1. An empty allowlist must pass `ValidateOnStart()` (an empty config is valid even if it makes every agent unknown).
2. The validator must read `IOptionsMonitor.CurrentValue` on each call so a runtime config update changes behavior between calls.

For (2) we need direct access to a mutable `TestOptionsMonitor`, so this test wires the validator manually rather than through DI.

- [ ] **Step 1: Append the failing tests**

Add to `AddSigilSecurityTests`:

```csharp
    [Fact]
    public void EmptyAllowlist_Passes_ValidateOnStart()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open"
            // no Keys
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        // ValidateOnStart triggers on first IOptions resolution.
        var opts = provider.GetRequiredService<IOptionsMonitor<SigilSecurityOptions>>().CurrentValue;

        opts.OpenTier.Keys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validator_PicksUp_OptionsMonitor_Reload()
    {
        var initial = new SigilSecurityOptions { Mode = SecurityTier.Open };
        initial.OpenTier.Keys["echo-agent"] = "old-key";

        var monitor = new TestOptionsMonitor<SigilSecurityOptions>(initial);
        var validator = new SigilKeyValidator(monitor, NullLogger<SigilKeyValidator>.Instance);

        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "new-key"
        };

        // Before reload: presented key doesn't match the configured "old-key".
        var beforeReload = await validator.AuthenticateAsync(creds, SecurityTier.Open);
        beforeReload.IsFailure.ShouldBeTrue();
        beforeReload.Error.ShouldBe(SigilSecurityErrors.KeyMismatch);

        // Reload: rotate the configured key to match what the agent will present.
        var rotated = new SigilSecurityOptions { Mode = SecurityTier.Open };
        rotated.OpenTier.Keys["echo-agent"] = "new-key";
        monitor.CurrentValue = rotated;

        // After reload: same call, different outcome.
        var afterReload = await validator.AuthenticateAsync(creds, SecurityTier.Open);
        afterReload.IsSuccess.ShouldBeTrue();
        afterReload.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
    }
```

- [ ] **Step 2: Run tests to verify they pass**

The validator already reads `_options.CurrentValue` on each call (per Task 10), so both tests should pass on first run.

```bash
dotnet test tests/Sigil.Infrastructure.Tests/Sigil.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AddSigilSecurityTests"
```

Expected: 5 tests pass total (3 prior + 2 new).

- [ ] **Step 3: Commit**

```bash
git add tests/Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests.cs
git commit -m "test(infra): assert empty allowlist boots and IOptionsMonitor reload is honored"
```

---

## Task 16: Final verification gate

**Files:** none (verification only)

- [ ] **Step 1: Full solution build**

```bash
dotnet build sigil.sln
```

Expected: clean build, zero warnings (any warning would fail the build under `TreatWarningsAsErrors=true`).

- [ ] **Step 2: Full test run**

```bash
dotnet test sigil.sln
```

Expected: every project's tests pass. The new `Sigil.Infrastructure.Tests` project should report:
- `SigilSecurityOptionsTests` — 3 tests
- `SigilKeyValidatorTests` — 14 tests (1 happy + 1 missing + 3 whitespace theory + 1 unknown + 1 wrong + 1 mode + 2 tier theory + 4 length theory)
- `AddSigilSecurityTests` — 5 tests

`Sigil.Core.Tests` should include the new files:
- `SecurityTierTests` — 7 tests
- `SigilCredentialsTests` — 4 tests
- `AuthenticationResultTests` — 3 tests
- `SigilSecurityErrorsTests` — 1 test
- Existing `SecurityProfileTests` — now 5 tests (3 original + 2 new)

- [ ] **Step 3: Verify no stray files**

```bash
git status
```

Expected: clean working tree (everything committed across Tasks 1–15).

- [ ] **Step 4: Tag the issue completion in the commit log**

If the orchestrating workflow wants a "closes #4" reference, do it in the PR description rather than amending commits. No final commit needed for this task — it's a verification gate only.

---

## Self-Review Notes

The plan was checked against the spec section by section:

| Spec section | Coverage |
|---|---|
| §2 In scope: Core security types | Tasks 1–4 |
| §2 In scope: `SecurityProfile.Tier` | Task 5 |
| §2 In scope: Infrastructure validator + options + DI | Tasks 6–10, 12, 14 |
| §2 In scope: `Sigil.Infrastructure.Tests` project | Task 8 |
| §2 In scope: Core test additions | Tasks 1, 2, 3, 4, 5 |
| §4 Contracts | Tasks 1–4 (matches code in spec verbatim) |
| §5 Validator behavior steps 1–6 | Step 1 (mode) → Task 12; step 2 (tier) → Task 12; step 3 (key normalize) → Task 10 (reused in 11); step 4 (lookup) → Task 10; step 5 (constant-time) → Task 10 + 13; step 6 (success log) → Task 10 |
| §5 DI extension | Task 14 |
| §7 Package additions | Tasks 6, 7, 8 |
| §8 Test plan — every bullet | Tasks 1, 2, 3, 5, 11, 12, 13, 14, 15 |
| §9 Verification gate | Task 16 |

No placeholders, no "TODO" / "TBD", no references to undefined types. Method/property names match across tasks: `AuthenticateAsync`, `SigilSecurityOptions.OpenTier.Keys`, `SecurityProfile.Tier`, `SigilSecurityErrors.MissingKey`/`UnknownAgent`/`KeyMismatch`/`TierNotSupported`/`ModeMismatch` are spelled identically wherever they appear.

---

## Execution Handoff

Plan complete and saved to `.bob/plans/2026-05-09-issue-04-sigil-key-validation-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
