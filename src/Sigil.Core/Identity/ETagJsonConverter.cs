using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class ETagJsonConverter : JsonConverter<ETag>
{
    public override ETag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException($"Cannot deserialize null into {nameof(ETag)}.");
        return new ETag(value);
    }

    public override void Write(Utf8JsonWriter writer, ETag value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
