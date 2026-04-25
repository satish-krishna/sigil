using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Identity;

public sealed class JobIdJsonConverter : JsonConverter<JobId>
{
    public override JobId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value is null)
            throw new JsonException($"Cannot deserialize null into {nameof(JobId)}.");
        return new JobId(value);
    }

    public override void Write(Utf8JsonWriter writer, JobId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
