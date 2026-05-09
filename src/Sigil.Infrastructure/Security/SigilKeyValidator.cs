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
