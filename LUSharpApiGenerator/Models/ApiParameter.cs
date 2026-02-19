using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiParameter
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Type")]
    public ApiValueType Type { get; set; } = new();

    [JsonPropertyName("Default")]
    public string? Default { get; set; }
}
