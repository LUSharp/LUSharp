using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiClass
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Superclass")]
    public string Superclass { get; set; } = "";

    [JsonPropertyName("Members")]
    public List<ApiMember> Members { get; set; } = new();

    [JsonPropertyName("Tags")]
    [JsonConverter(typeof(TagsConverter))]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("MemoryCategory")]
    public string? MemoryCategory { get; set; }

    // Resolved at filter time
    [JsonIgnore]
    public bool IsNotCreatable => Tags?.Contains("NotCreatable") == true;

    [JsonIgnore]
    public bool IsService => Tags?.Contains("Service") == true;
}
