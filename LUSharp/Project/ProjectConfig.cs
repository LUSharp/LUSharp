using Newtonsoft.Json;

namespace LUSharp.Project;

public class ProjectConfig
{
    [JsonProperty("name")]
    public string Name { get; set; } = "MyProject";

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonProperty("packages")]
    public List<string> Packages { get; set; } = new();

    [JsonProperty("build")]
    public BuildConfig Build { get; set; } = new();

    public static ProjectConfig FromJson(string json) =>
        JsonConvert.DeserializeObject<ProjectConfig>(json)!;

    public string ToJson() =>
        JsonConvert.SerializeObject(this, Formatting.Indented);

    public static ProjectConfig LoadFromDirectory(string dir)
    {
        var path = Path.Combine(dir, "lusharp.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"lusharp.json not found in {dir}");
        return FromJson(File.ReadAllText(path));
    }
}

public class BuildConfig
{
    [JsonProperty("src")]  public string Src { get; set; } = "./src";
    [JsonProperty("out")]  public string Out { get; set; } = "./out";
}
