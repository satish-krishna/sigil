using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class ETagJsonConverter : JsonConverter<ETag>
{
    public override ETag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, ETag value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
