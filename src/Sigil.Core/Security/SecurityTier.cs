using System.Text.Json.Serialization;

namespace Sigil.Core.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SecurityTier
{
    Open,
    Standard,
    Trusted
}
