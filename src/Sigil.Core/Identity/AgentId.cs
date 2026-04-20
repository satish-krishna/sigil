using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

[JsonConverter(typeof(AgentIdJsonConverter))]
public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
