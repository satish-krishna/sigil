using Sigil.Core.Identity;

namespace Sigil.Core.Security;

public sealed record SigilCredentials
{
    public required AgentId AgentId { get; init; }
    public string? SigilKey { get; init; }
    public string? Jwt { get; init; }
    public string? CertificateThumbprint { get; init; }

    public bool Equals(SigilCredentials? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AgentId == other.AgentId
            && SigilKey == other.SigilKey
            && Jwt == other.Jwt
            && CertificateThumbprint == other.CertificateThumbprint;
    }

    public override int GetHashCode() =>
        HashCode.Combine(AgentId, SigilKey, Jwt, CertificateThumbprint);
}
