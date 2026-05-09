namespace Sigil.Core.Registry;

public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    public bool Equals(SecurityProfile? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CertificateThumbprint == other.CertificateThumbprint
            && SigilKey == other.SigilKey
            && IsPiiCleared == other.IsPiiCleared
            && AllowedTools.SequenceEqual(other.AllowedTools);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CertificateThumbprint);
        hash.Add(SigilKey);
        hash.Add(IsPiiCleared);
        foreach (var tool in AllowedTools) hash.Add(tool);
        return hash.ToHashCode();
    }
}
