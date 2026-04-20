using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class StepIdJsonConverter : JsonConverter<StepId>
{
    public override StepId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, StepId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
