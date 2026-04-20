using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class StepIdJsonConverter : JsonConverter<StepId>
{
    public override StepId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException($"Cannot deserialize null into {nameof(StepId)}.");
        return new StepId(value);
    }

    public override void Write(Utf8JsonWriter writer, StepId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
