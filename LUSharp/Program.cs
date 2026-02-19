using LUSharp.Project;
using LUSharp.Project.Rojo;
using LUSharpTranspiler.Build;

namespace LUSharp
{
    internal class Program
    {
        private static Dictionary<string, (string, string)> commands = new()
        {
            {"help", ("command_name(optional)", "List usage for all or a specific command.")},
            {"new", ("project_name[REQUIRED]", "Create a new project with the name project_name.") },
            {"build", ("[project_dir] [--out=path] [--release]", "Transpile C# source to Luau output.") }
        };

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Logger.Log(Logger.LogSeverity.Error, "Please use LUSharp.exe help for more options.");
                return;
            }

            switch (args[0])
            {
                case "help":
                    {
                        if (args.Length > 1)
                            PrintHelp(args[1] ?? "");
                        else
                            PrintHelp();
                        break;
                    }
                case "new":
                    {
                        // ensure we have project name
                        if (args.Length < 2)
                        {
                            PrintHelp();
                            return;
                        }
                        // create project
                        CreateProject(args[1]);
                        break;
                    }
                case "build":
                    {
                        var dir = args.Length > 1 && !args[1].StartsWith("--")
                            ? args[1]
                            : Directory.GetCurrentDirectory();
                        var outFlag = args.FirstOrDefault(a => a.StartsWith("--out="))?[6..];
                        var release = args.Contains("--release");
                        Environment.ExitCode = BuildCommand.Run(dir, outFlag, release);
                        break;
                    }
                default:
                    PrintHelp();
                    break;
            }
        }

        public static void PrintHelp(string name = "")
        {
            if (string.IsNullOrEmpty(name))
                foreach (var cmd in commands)
                {
                    Console.WriteLine($"{cmd.Key} {cmd.Value.Item1}\n\t-{cmd.Value.Item2}");
                }
            else
            {
                var cmd = commands[name];
                Console.WriteLine($"{name} {cmd.Item1}\n\t-{cmd.Item2}");
            }
        }

        public static void CreateProject(string name)
        {
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), name)))
            {
                Logger.Log(Logger.LogSeverity.Error, "Project in directory exists.");
                return;
            }

            ProjectScaffolder.Scaffold(name);
        }
    }
}
