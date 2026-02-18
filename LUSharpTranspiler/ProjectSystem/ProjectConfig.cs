using Newtonsoft.Json;
namespace LUSharp.ProjectSystem
{
    /// <summary>
    /// Rojo compatible project configuration.
    /// </summary>
    public class ProjectConfig
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("globIgnorePaths")]
        public List<string> GlobIgnorePaths { get; set; }

        [JsonProperty("tree")]
        public RojoNode Tree { get; set; }
    }

    public class RojoNode
    {
        // The Roblox class name
        [JsonProperty("$className")]
        public string ClassName { get; set; }

        // Optional path field
        [JsonProperty("$path")]
        public string Path { get; set; }

        // Optional properties block
        [JsonProperty("$properties")]
        public Dictionary<string, object> Properties { get; set; }

        // Child nodes
        // Any unknown key (e.g. "ServerScriptService") is treated as a child node
        [JsonExtensionData]
        public Dictionary<string, object> Children { get; set; }
    }
}