using System.Text.Json.Serialization;

namespace LUSharpApiGenerator.Models;

public class ApiCallback : ApiMember
{
    [JsonPropertyName("ReturnType")]
    [JsonConverter(typeof(ReturnTypeConverter))]
    public ApiReturnType ReturnType { get; set; } = new();

    [JsonPropertyName("Parameters")]
    public List<ApiParameter> Parameters { get; set; } = new();

    [JsonPropertyName("Security")]
    public string Security { get; set; } = "None";
}
