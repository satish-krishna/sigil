namespace Sigil.Core.Registry;

public sealed record SecurityProfile
{
    public string? CertificateThumbprint { get; init; }
    public string? SigilKey { get; init; }
    public bool IsPiiCleared { get; init; }
    public string[] AllowedTools { get; init; } = [];
}
