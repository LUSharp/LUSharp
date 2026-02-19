using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiEvent : ApiMember
{
    [JsonPropertyName("Parameters")]
    public List<ApiParameter> Parameters { get; set; } = new();

    [JsonPropertyName("Security")]
    public string Security { get; set; } = "None";
}
