using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

[JsonConverter(typeof(JobIdJsonConverter))]
public readonly record struct JobId(string Value)
{
    public override string ToString() => Value;
}
