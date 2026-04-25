using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

[JsonConverter(typeof(StepIdJsonConverter))]
public readonly record struct StepId(string Value)
{
    public override string ToString() => Value;
}
