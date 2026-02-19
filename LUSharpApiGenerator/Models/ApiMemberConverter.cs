using System.Text.Json;
using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiMemberConverter : JsonConverter<ApiMember>
{
    public override ApiMember Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var memberType = root.GetProperty("MemberType").GetString();
        var raw = root.GetRawText();

        // Create options without ApiMemberConverter to avoid infinite recursion
        // (ApiMember is the base class of all concrete member types)
        var innerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            AllowTrailingCommas = options.AllowTrailingCommas
        };

        return memberType switch
        {
            "Property" => JsonSerializer.Deserialize<ApiProperty>(raw, innerOptions)!,
            "Function" => JsonSerializer.Deserialize<ApiFunction>(raw, innerOptions)!,
            "Event" => JsonSerializer.Deserialize<ApiEvent>(raw, innerOptions)!,
            "Callback" => JsonSerializer.Deserialize<ApiCallback>(raw, innerOptions)!,
            _ => throw new JsonException($"Unknown MemberType: {memberType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ApiMember value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
