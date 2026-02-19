using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public abstract class ApiMember
{
    [JsonPropertyName("MemberType")]
    public string MemberType { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Tags")]
    [JsonConverter(typeof(TagsConverter))]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("ThreadSafety")]
    public string? ThreadSafety { get; set; }

    public bool HasTag(string tag) => Tags?.Contains(tag) == true;
}
