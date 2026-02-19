using Newtonsoft.Json;

namespace LUSharp.Project.Rojo
{
    public class RojoInstance
    {
        [JsonProperty("$className", NullValueHandling = NullValueHandling.Ignore)]
        public string ClassName { get; set; }

        [JsonProperty("$path", NullValueHandling = NullValueHandling.Ignore)]
        public string Path { get; set; }

        [JsonProperty("$properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Properties { get; set; }

        // NOT serialized directly
        [JsonIgnore]
        public Dictionary<string, RojoInstance> Children { get; set; } = new();
    }

    public class RojoConfig
    {
        public string name { get; set; }

        public RojoInstance tree { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? servePort { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int> servePlaceIds { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> globIgnorePaths { get; set; }
    }
}
