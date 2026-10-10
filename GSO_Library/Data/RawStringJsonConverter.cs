using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSO_Library.Data;

// Reads strings verbatim. Apply via [JsonConverter] to opt a property (e.g. passwords) out of the
// global TrimmingStringJsonConverter — property-level converters take precedence over global ones.
public class RawStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string value but got {reader.TokenType}.");
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
