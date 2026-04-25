using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

[JsonConverter(typeof(ETagJsonConverter))]
public readonly record struct ETag(string Value)
{
    public override string ToString() => Value;
}
