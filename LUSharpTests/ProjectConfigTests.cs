using LUSharp.Project;

namespace LUSharpTests;

public class ProjectConfigTests
{
    [Fact]
    public void Deserialize_ValidJson()
    {
        var json = """
            {
              "name": "MyGame",
              "packages": ["ECS"],
              "build": { "src": "./src", "out": "./out" }
            }
            """;

        var config = ProjectConfig.FromJson(json);
        Assert.Equal("MyGame", config.Name);
        Assert.Contains("ECS", config.Packages);
        Assert.Equal("./src", config.Build.Src);
    }

    [Fact]
    public void Serialize_RoundTrips()
    {
        var config = new ProjectConfig { Name = "Test", Packages = new() { "ECS" } };
        var json = config.ToJson();
        var back = ProjectConfig.FromJson(json);
        Assert.Equal("Test", back.Name);
    }
}
