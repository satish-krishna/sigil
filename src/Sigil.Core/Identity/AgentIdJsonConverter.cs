using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class AgentIdJsonConverter : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException($"Cannot deserialize null into {nameof(AgentId)}.");
        return new AgentId(value);
    }

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
