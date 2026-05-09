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
