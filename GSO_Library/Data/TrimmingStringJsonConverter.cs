using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSO_Library.Data;

// Trims leading/trailing whitespace from every string deserialized from a request body,
// so accidental whitespace (e.g. pasted names) never reaches the database.
public class TrimmingStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string value but got {reader.TokenType}.");
        return reader.GetString()?.Trim();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
