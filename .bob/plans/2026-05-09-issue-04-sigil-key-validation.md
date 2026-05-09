# Issue #4 — Sigil-Key validation (Open tier)

> Phase 1 · Security. Minimal pre-shared-key authentication for local/dev. JWT and mTLS arrive in Phase 3.
>
> Blueprint: `.bob/docs/sigil-architecture-blueprint.md` §4.1, §4.7, §5, §7.2.
> GitHub: <https://github.com/satish-krishna/sigil/issues/4>.

---

## 1. Goal

Wire enough of the Zero-Trust security model into the kernel that an Open-tier registration flow can reject a missing or invalid Sigil-Key, while leaving room for Standard (JWT) and Trusted (mTLS) tiers to plug in behind the same abstraction in Phase 3.

The kernel-configured allowlist — keyed by `AgentId` — is the source of truth for what an Open-tier agent must present. The registry never persists the secret.

## 2. Scope

### In scope

- `Sigil.Core/Security/`: `SecurityTier`, `SigilCredentials`, `AuthenticationResult`, `ISigilSecurity`, `SigilSecurityErrors`.
- `Sigil.Core/Registry/SecurityProfile.cs`: add `SecurityTier Tier` (default `Open`); update `Equals`/`GetHashCode`.
- `Sigil.Infrastructure/Security/`: `SigilSecurityOptions`, `SigilKeyValidator` (the Phase 1 implementation of `ISigilSecurity`), `ServiceCollectionExtensions.AddSigilSecurity`.
- New test project `tests/Sigil.Infrastructure.Tests/` (xUnit + Shouldly).
- New tests under `tests/Sigil.Core.Tests/Security/` and additions to `Registry/SecurityProfileTests.cs`.

### Out of scope (deferred to other issues)

- `POST /api/agents/register` HTTP endpoint — Issue #13.
- Heartbeat / deregister authn — Issues #11 / #13 will reuse `ISigilSecurity`.
- JWT issuance + refresh — Phase 3 / Issue #9.
- mTLS support — Phase 3 / Issue #10.
- Persisting the presented key (hashed or otherwise) on `AgentRegistration`. The existing nullable `SecurityProfile.SigilKey` field is left as-is and is not populated by the kernel at Open tier; an XML doc-comment on the field will note this.

## 3. Design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Source of truth for Open-tier keys | Static allowlist in kernel config, keyed by `AgentId` | Matches the docker-compose convention (`Security__Mode=Open`, `Sigil__SigilKey=dev-key-echo`). Per-agent keying prevents an agent from impersonating another even at the lowest tier. |
| Wire transport for the key | `X-Sigil-Key` HTTP header | Keeps the secret out of the JSON body and out of structured logs. The body's existing `SecurityProfile.SigilKey` field is not consumed on inbound. |
| Persistence on `SecurityProfile` after a successful registration | `Tier` only | Allowlist remains source of truth. Avoids duplicating a secret in the registry; aligns with Phase 3 plans for hashed/JWT-derived credentials. |
| `ISigilSecurity` shape | Single facade with internal tier dispatch | Phase 1 binds `SigilKeyValidator` as the impl; Phase 3 swaps in a JWT/mTLS-aware impl. Callers never branch on tier. |
| Credentials shape | Generic `SigilCredentials` bag | Forward-compatible: Standard/Trusted tiers populate additional fields without changing callers or the interface. |
| Failure model | `Result<AuthenticationResult>` (CSharpFunctionalExtensions) with short string reason codes | Project already uses `Result`; string codes give callers stable, log-safe matching without exception machinery. |
| Issue boundary | Ship abstractions + validator + tests; no HTTP endpoint | The `/api/agents/register` endpoint is owned by #13; this issue stops at the seam #13 will plug into. |

## 4. Contracts (new code in `Sigil.Core/Security/`)

```csharp
namespace Sigil.Core.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityTier { Open, Standard, Trusted }

public sealed record SigilCredentials
{
    public required AgentId AgentId { get; init; }
    public string? SigilKey { get; init; }
    public string? Jwt { get; init; }                    // populated in Phase 3
    public string? CertificateThumbprint { get; init; }  // populated in Phase 3
}

public sealed record AuthenticationResult
{
    public required AgentId AgentId { get; init; }
    public required SecurityTier Tier { get; init; }
}

public interface ISigilSecurity
{
    Task<Result<AuthenticationResult>> AuthenticateAsync(
        SigilCredentials credentials,
        SecurityTier requiredTier,
        CancellationToken ct = default);
}

public static class SigilSecurityErrors
{
    public const string MissingKey       = "missing-key";
    public const string UnknownAgent     = "unknown-agent";
    public const string KeyMismatch      = "key-mismatch";
    public const string TierNotSupported = "tier-not-supported";
    public const string ModeMismatch     = "mode-mismatch";
}
```

`SigilCredentials` is a transport DTO — empty/whitespace `SigilKey` is normalized to `null` inside the validator so missing and blank both surface as `missing-key`.

`requiredTier` lets a future operation demand a stricter tier than the kernel's configured baseline (e.g., a Trusted-only write surface). For Phase 1 the registration path always passes `requiredTier = Open`.

### `SecurityProfile` patch

```csharp
public SecurityTier Tier { get; init; } = SecurityTier.Open;
```

`Equals`/`GetHashCode` updated to include `Tier`. The kernel sets this field when persisting the registration — clients populating it on the wire are ignored. The existing `SigilKey` field gets an XML doc-comment noting that it is **not** persisted by the kernel at Open tier (allowlist remains the source of truth) so future readers don't mistake the empty field for a bug.

## 5. Validator (`Sigil.Infrastructure/Security/`)

### Options

```csharp
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

Sample binding (documented; secrets stay out of git):

```json
{
  "Security": {
    "Mode": "Open",
    "OpenTier": {
      "Keys": { "echo-agent": "dev-key-echo" }
    }
  }
}
```

Maps cleanly to the existing docker-compose env vars (`Security__Mode=Open`, `Sigil__SigilKey=dev-key-echo`).

### `SigilKeyValidator`

Implements `ISigilSecurity` with this contract:

1. If `options.Mode != SecurityTier.Open` → `mode-mismatch`. Phase 3 will register a different `ISigilSecurity` impl when the kernel boots in Standard/Trusted mode; this branch ensures a misconfigured kernel fails loudly today.
2. If `requiredTier != SecurityTier.Open` → `tier-not-supported`. The Open validator can't satisfy a stricter requirement.
3. Normalize `credentials.SigilKey` (whitespace → null). If null → `missing-key`.
4. Lookup `credentials.AgentId.Value` in `options.OpenTier.Keys`. Miss → `unknown-agent`.
5. Compare the presented key to the configured key with `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes. `FixedTimeEquals` requires equal-length spans, so when lengths differ run a constant-time comparison against a same-length sentinel buffer (filled from the longer of the two) and discard the result, then report `key-mismatch`. The intent is that mismatch processing time is independent of *where* a difference occurs and approximately independent of length divergence. Match → success.
6. On success: log `"Open-tier auth OK for {AgentId}"` at Information; return `Result.Success(new AuthenticationResult { AgentId, Tier = Open })`.

Failure logging records the reason code and `AgentId` only — never the presented or expected key.

The validator depends on `IOptionsMonitor<SigilSecurityOptions>` (so dev config reloads take effect without restart) and `ILogger<SigilKeyValidator>`. It is registered as a singleton.

### DI extension

```csharp
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
```

Empty `OpenTier.Keys` is permitted — it's a valid (if useless) state where every registration produces `unknown-agent`.

## 6. File layout

```
src/Sigil.Core/Security/
├── SecurityTier.cs
├── SigilCredentials.cs
├── AuthenticationResult.cs
├── ISigilSecurity.cs
└── SigilSecurityErrors.cs

src/Sigil.Core/Registry/SecurityProfile.cs        (patched)

src/Sigil.Infrastructure/Security/
├── SigilSecurityOptions.cs
├── SigilKeyValidator.cs
└── ServiceCollectionExtensions.cs

tests/Sigil.Core.Tests/Security/
├── SecurityTierTests.cs
├── SigilCredentialsTests.cs
└── AuthenticationResultTests.cs

tests/Sigil.Core.Tests/Registry/SecurityProfileTests.cs   (extended)

tests/Sigil.Infrastructure.Tests/                  (new project)
├── Sigil.Infrastructure.Tests.csproj
└── Security/
    ├── SigilKeyValidatorTests.cs
    └── AddSigilSecurityTests.cs
```

## 7. Package additions

`Directory.Packages.props` gains:

- `Microsoft.Extensions.Options.ConfigurationExtensions` (for `Bind` / `IOptionsMonitor` wiring on `Sigil.Infrastructure`).
- `Microsoft.Extensions.Logging.Abstractions` (for `ILogger<T>` on `Sigil.Infrastructure`).
- `Microsoft.Extensions.Configuration` + `Microsoft.Extensions.Configuration.Binder` for the new test project (in-memory configuration construction).
- `Microsoft.Extensions.DependencyInjection` for the new test project.

Pinned versions follow the existing `9.0.x` line used elsewhere in the solution. `Sigil.Core` takes no new dependencies.

## 8. Test plan (TDD; Shouldly assertions)

### `Sigil.Core.Tests/Security/`

- `SecurityTierTests` — JSON round-trip emits `"Open"` / `"Standard"` / `"Trusted"`; default value is `Open`.
- `SigilCredentialsTests` — equality (including all-null optional fields), JSON round-trip preserves nullable fields, AgentId required.
- `AuthenticationResultTests` — equality, JSON round-trip.

### `Sigil.Core.Tests/Registry/SecurityProfileTests` (extended)

- Default `Tier` is `SecurityTier.Open`.
- Two profiles differing only in `Tier` are not equal.
- Two profiles with identical fields including `Tier` produce equal hash codes.

### `Sigil.Infrastructure.Tests/Security/SigilKeyValidatorTests`

- `MissingKey` — `SigilKey == null` → `Result.Failure` with `missing-key`.
- `WhitespaceKey` — `SigilKey == "  "` → `missing-key`.
- `UnknownAgent` — AgentId absent from allowlist → `unknown-agent`.
- `WrongKey` — AgentId present, key differs → `key-mismatch`.
- `CorrectKey` — match → `Result.Success` with `AgentId` echoed and `Tier = Open`.
- `ModeMisconfigured` — `Mode = Standard` → `mode-mismatch` regardless of credentials.
- `TierEscalationRefused` — `requiredTier = Trusted` → `tier-not-supported`.
- `KeyComparisonHandlesLengthDifferences` — calling the validator with a key one byte longer or shorter than the configured value still returns `key-mismatch` (no exception from `FixedTimeEquals`'s length check leaking through). This asserts the same-length-sentinel handling described in §5; we do not attempt a statistical timing benchmark.

### `Sigil.Infrastructure.Tests/Security/AddSigilSecurityTests`

- After `services.AddSigilSecurity(config).BuildServiceProvider()`, `ISigilSecurity` resolves as `SigilKeyValidator`.
- Options bind correctly from an in-memory `IConfiguration`.
- Empty `OpenTier.Keys` passes `ValidateOnStart()`.
- A registered validator survives an `IOptionsMonitor` reload (config update changes the active allowlist between two `AuthenticateAsync` calls).

## 9. Verification gate

`dotnet build sigil.sln && dotnet test sigil.sln` must pass. `TreatWarningsAsErrors=true` (from `Directory.Build.props`) catches sloppy nullability / unused usings.

## 10. Rollout / integration notes for downstream issues

- **Issue #13 (FastEndpoints — agent lifecycle):** the `POST /api/agents/register` endpoint reads the `X-Sigil-Key` header, builds a `SigilCredentials { AgentId, SigilKey }`, calls `ISigilSecurity.AuthenticateAsync(creds, SecurityTier.Open)`. On success it sets `registration with { Security = registration.Security with { Tier = Open } }` (and clears `SigilKey` if present in the body) before calling `IAgentRegistrationStore.RegisterAsync`. On failure it returns 401 with the reason code in the problem-details body.
- **Issue #9 (SDK — registration / heartbeat / JWT refresh):** the SDK reads its own key from configuration (the existing `Sigil__SigilKey` env var) and attaches it as `X-Sigil-Key` on the registration request. Heartbeat reuses the same header until JWT lands.
- **Phase 3 / Issue #10 (Secure Gateway):** a new `JwtSigilValidator` (or a composite) replaces `SigilKeyValidator` as the bound `ISigilSecurity`; the configured `Mode` switches to `Standard` or `Trusted`. The credentials bag already carries `Jwt` and `CertificateThumbprint` for that day.

## 11. Open questions

None blocking. The shape of Phase 3's composite validator (chain vs. swap) can be decided when #9 and #10 land — `ISigilSecurity` as a single facade keeps both options open.
