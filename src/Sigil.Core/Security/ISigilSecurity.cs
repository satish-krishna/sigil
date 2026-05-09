using CSharpFunctionalExtensions;

namespace Sigil.Core.Security;

public interface ISigilSecurity
{
    Task<Result<AuthenticationResult>> AuthenticateAsync(
        SigilCredentials credentials,
        SecurityTier requiredTier,
        CancellationToken ct = default);
}
