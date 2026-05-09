using Sigil.Core.Identity;

namespace Sigil.Core.Security;

public sealed record SigilCredentials
{
    public required AgentId AgentId { get; init; }
    public string? SigilKey { get; init; }
    public string? Jwt { get; init; }
    public string? CertificateThumbprint { get; init; }
}
