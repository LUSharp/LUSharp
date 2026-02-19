using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiDump
{
    [JsonPropertyName("Version")]
    public int Version { get; set; }

    [JsonPropertyName("Classes")]
    public List<ApiClass> Classes { get; set; } = new();

    [JsonPropertyName("Enums")]
    public List<ApiEnum> Enums { get; set; } = new();
}

public class ApiEnum
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Items")]
    public List<ApiEnumItem> Items { get; set; } = new();

    [JsonPropertyName("Tags")]
    [JsonConverter(typeof(TagsConverter))]
    public List<string>? Tags { get; set; }
}

public class ApiEnumItem
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Value")]
    public int Value { get; set; }

    [JsonPropertyName("Tags")]
    [JsonConverter(typeof(TagsConverter))]
    public List<string>? Tags { get; set; }
}
