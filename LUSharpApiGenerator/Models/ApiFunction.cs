using System.Text.Json;
using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiFunction : ApiMember
{
    [JsonPropertyName("ReturnType")]
    [JsonConverter(typeof(ReturnTypeConverter))]
    public ApiReturnType ReturnType { get; set; } = new();

    [JsonPropertyName("Parameters")]
    public List<ApiParameter> Parameters { get; set; } = new();

    [JsonPropertyName("Security")]
    public string Security { get; set; } = "None";
}

/// <summary>
/// Handles both single return type {"Category":"..","Name":".."} and
/// tuple returns [{"Category":"..","Name":".."},{"Category":"..","Name":".."}]
/// </summary>
public class ApiReturnType
{
    public List<ApiValueType> Types { get; set; } = new();

    public bool IsTuple => Types.Count > 1;

    // Convenience for single return
    public ApiValueType Single => Types.Count > 0 ? Types[0] : new ApiValueType { Name = "void", Category = "Primitive" };
}

public class ReturnTypeConverter : JsonConverter<ApiReturnType>
{
    public override ApiReturnType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new ApiReturnType();

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var vt = JsonSerializer.Deserialize<ApiValueType>(ref reader, options);
            if (vt != null) result.Types.Add(vt);
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    var vt = JsonSerializer.Deserialize<ApiValueType>(ref reader, options);
                    if (vt != null) result.Types.Add(vt);
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, ApiReturnType value, JsonSerializerOptions options)
    {
        if (value.IsTuple)
            JsonSerializer.Serialize(writer, value.Types, options);
        else
            JsonSerializer.Serialize(writer, value.Single, options);
    }
}
