using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiProperty : ApiMember
{
    [JsonPropertyName("ValueType")]
    public ApiValueType ValueType { get; set; } = new();

    [JsonPropertyName("Security")]
    public ApiPropertySecurity Security { get; set; } = new();

    [JsonPropertyName("Category")]
    public string? Category { get; set; }

    [JsonPropertyName("Default")]
    public string? Default { get; set; }

    [JsonPropertyName("Serialization")]
    public ApiSerialization? Serialization { get; set; }

    public bool IsReadOnly => HasTag("ReadOnly");
}

public class ApiPropertySecurity
{
    [JsonPropertyName("Read")]
    public string Read { get; set; } = "None";

    [JsonPropertyName("Write")]
    public string Write { get; set; } = "None";
}

public class ApiSerialization
{
    [JsonPropertyName("CanLoad")]
    public bool CanLoad { get; set; }

    [JsonPropertyName("CanSave")]
    public bool CanSave { get; set; }
}
