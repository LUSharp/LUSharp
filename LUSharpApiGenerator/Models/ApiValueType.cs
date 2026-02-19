using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiValueType
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Category")]
    public string Category { get; set; } = "";
}
