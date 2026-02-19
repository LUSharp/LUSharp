using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LUSharp.Project.Rojo;
using LUSharpTranspiler.Build;
using Newtonsoft.Json;

namespace LUSharp.Project;

public static class ProjectScaffolder
{
    public static void Scaffold(string name)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), name);

        // --- Directories ---
        Directory.CreateDirectory(Path.Combine(root, "src", "client"));
        Directory.CreateDirectory(Path.Combine(root, "src", "server"));
        Directory.CreateDirectory(Path.Combine(root, "src", "shared"));
        Directory.CreateDirectory(Path.Combine(root, "lib"));
        Directory.CreateDirectory(Path.Combine(root, "out"));

        // --- Template C# files ---
        WriteTemplate("LUSharp.Example.ClientMain.cs",
            Path.Combine(root, "src", "client", "ClientMain.cs"), name);
        WriteTemplate("LUSharp.Example.ServerMain.cs",
            Path.Combine(root, "src", "server", "ServerMain.cs"), name);
        WriteTemplate("LUSharp.Example.SharedMain.cs",
            Path.Combine(root, "src", "shared", "SharedModule.cs"), name);

        // --- .csproj ---
        File.WriteAllText(Path.Combine(root, $"{name}.csproj"), GenerateCsproj());

        // --- .sln ---
        File.WriteAllText(Path.Combine(root, $"{name}.sln"), GenerateSln(name));

        // --- lusharp.json ---
        var config = new ProjectConfig { Name = name };
        File.WriteAllText(Path.Combine(root, "lusharp.json"), config.ToJson());

        // --- default.project.json (Rojo) ---
        WriteRojoConfig(root, name);

        // --- .gitignore ---
        File.WriteAllText(Path.Combine(root, ".gitignore"),
            "bin/\nobj/\nout/\n*.user\n");

        // --- LUSharpAPI.dll ---
        BundleApiDll(root);

        Logger.Log($"Project '{name}' created successfully.");
        Logger.Log($"  cd {name}");
        Logger.Log($"  dotnet build        # verify intellisense");
        Logger.Log($"  lusharp build       # transpile to Luau");
    }

    private static void WriteTemplate(string resourceName, string dest, string projectName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.Log(Logger.LogSeverity.Warning, $"Template resource '{resourceName}' not found, skipping.");
            return;
        }
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().Replace("YOURPROJECT", projectName);
        File.WriteAllText(dest, content);
    }

    private static string GenerateCsproj()
    {
        return """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <OutputType>Library</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <Reference Include="LUSharpAPI">
                  <HintPath>lib\LUSharpAPI.dll</HintPath>
                </Reference>
              </ItemGroup>

            </Project>
            """;
    }

    private static string GenerateSln(string name)
    {
        // Deterministic project GUID derived from the project name
        var projectGuid = DeterministicGuid(name).ToString("B").ToUpperInvariant();
        var slnGuid = DeterministicGuid(name + ".sln").ToString("B").ToUpperInvariant();

        return $$"""

            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}", "{{name}}.csproj", "{{projectGuid}}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{{projectGuid}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{{projectGuid}}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{{projectGuid}}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{{projectGuid}}.Release|Any CPU.Build.0 = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(SolutionProperties) = preSolution
            		HideSolutionNode = FALSE
            	EndGlobalSection
            	GlobalSection(ExtensibilityGlobals) = postSolution
            		SolutionGuid = {{slnGuid}}
            	EndGlobalSection
            EndGlobal
            """;
    }

    private static void WriteRojoConfig(string root, string name)
    {
        var config = new RojoConfig
        {
            name = name,
            globIgnorePaths = new List<string>
            {
                "**/*.csproj",
                "**/*.sln",
                "**/bin/**",
                "**/obj/**"
            },
            tree = new RojoInstance
            {
                ClassName = "DataModel",
                Children = new Dictionary<string, RojoInstance>
                {
                    ["HttpService"] = new RojoInstance
                    {
                        ClassName = "HttpService",
                        Properties = new Dictionary<string, object>
                        {
                            ["HttpEnabled"] = true
                        }
                    },
                    ["ServerScriptService"] = new RojoInstance
                    {
                        ClassName = "ServerScriptService",
                        Path = "out/server"
                    },
                    ["ReplicatedStorage"] = new RojoInstance
                    {
                        ClassName = "ReplicatedStorage",
                        Children = new Dictionary<string, RojoInstance>
                        {
                            ["Shared"] = new RojoInstance { Path = "out/shared" },
                            ["Runtime"] = new RojoInstance { Path = "out/runtime" }
                        }
                    },
                    ["StarterPlayer"] = new RojoInstance
                    {
                        ClassName = "StarterPlayer",
                        Children = new Dictionary<string, RojoInstance>
                        {
                            ["StarterPlayerScripts"] = new RojoInstance
                            {
                                ClassName = "StarterPlayerScripts",
                                Path = "out/client"
                            }
                        }
                    }
                }
            }
        };

        RojoManager.WriteConfig(Path.Combine(root, "default.project.json"), config);
    }

    private static void BundleApiDll(string root)
    {
        var dllName = "LUSharpAPI.dll";
        var source = Path.Combine(AppContext.BaseDirectory, dllName);
        var dest = Path.Combine(root, "lib", dllName);

        if (File.Exists(source))
        {
            File.Copy(source, dest, overwrite: true);
        }
        else
        {
            Logger.Log(Logger.LogSeverity.Warning,
                $"LUSharpAPI.dll not found at '{source}'. Intellisense will not work until you place the DLL in lib/.");
        }
    }

    private static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
