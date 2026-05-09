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
