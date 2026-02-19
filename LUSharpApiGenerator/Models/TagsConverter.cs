using System.Text.Json;
using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

/// <summary>
/// Tags arrays can contain both strings and objects.
/// We only care about the string tags — objects are metadata we skip.
/// </summary>
public class TagsConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for Tags");

        var tags = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return tags;

            if (reader.TokenType == JsonTokenType.String)
            {
                tags.Add(reader.GetString()!);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Skip the entire object
                reader.Skip();
            }
        }

        return tags;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var tag in value)
            writer.WriteStringValue(tag);
        writer.WriteEndArray();
    }
}
