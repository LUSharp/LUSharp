using System.Reflection;
using System.Text.RegularExpressions;
using LUSharp.Project;
using LUSharp.Project.Rojo;
using LUSharpTranspiler.Build;

namespace LUSharp
{
    internal class Program
    {
        private static readonly Dictionary<string, (string, string)> commands = new()
        {
            {"help", ("command_name(optional)", "List usage for all or a specific command.")},
            {"new", ("project_name[REQUIRED]", "Create a new project with the name project_name.") },
            {"build", ("[project_dir] [--out=path] [--release]", "Transpile C# source to Luau output.") }
        };

        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    Logger.Log(Logger.LogSeverity.Error, "No command specified. Run 'lusharp help' for available commands.");
                    return 1;
                }

                switch (args[0])
                {
                    case "--version":
                    case "-v":
                        {
                            var version = Assembly.GetExecutingAssembly().GetName().Version;
                            Console.WriteLine($"lusharp {version?.ToString(3) ?? "0.0.0"}");
                            return 0;
                        }
                    case "help":
                        {
                            if (args.Length > 1)
                                PrintHelp(args[1]);
                            else
                                PrintHelp();
                            return 0;
                        }
                    case "new":
                        {
                            if (args.Length < 2)
                            {
                                Logger.Log(Logger.LogSeverity.Error, "Missing project name. Usage: lusharp new <project_name>");
                                return 1;
                            }
                            return CreateProject(args[1]);
                        }
                    case "build":
                        {
                            var dir = args.Length > 1 && !args[1].StartsWith("--")
                                ? args[1]
                                : Directory.GetCurrentDirectory();
                            var outFlag = args.FirstOrDefault(a => a.StartsWith("--out="))?[6..];
                            var release = args.Contains("--release");
                            return BuildCommand.Run(dir, outFlag, release);
                        }
                    default:
                        Logger.Log(Logger.LogSeverity.Error, $"Unknown command '{args[0]}'. Run 'lusharp help' for available commands.");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.LogSeverity.Error, $"Unexpected error: {ex.Message}");
                return 1;
            }
        }

        public static void PrintHelp(string name = "")
        {
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Usage: lusharp <command> [options]\n");
                Console.WriteLine("Commands:");
                foreach (var cmd in commands)
                {
                    Console.WriteLine($"  {cmd.Key,-10} {cmd.Value.Item1}");
                    Console.WriteLine($"             {cmd.Value.Item2}");
                }
                Console.WriteLine("\nFlags:");
                Console.WriteLine("  --version, -v   Print the LUSharp version");
            }
            else
            {
                if (!commands.ContainsKey(name))
                {
                    Logger.Log(Logger.LogSeverity.Error, $"Unknown command '{name}'.");
                    Console.WriteLine("\nAvailable commands:");
                    foreach (var c in commands)
                        Console.WriteLine($"  {c.Key,-10} {c.Value.Item2}");
                    return;
                }
                var entry = commands[name];
                Console.WriteLine($"lusharp {name} {entry.Item1}\n  {entry.Item2}");
            }
        }

        public static int CreateProject(string name)
        {
            if (!IsValidProjectName(name))
            {
                Logger.Log(Logger.LogSeverity.Error,
                    $"Invalid project name '{name}'. Must start with a letter and contain only letters, digits, or underscores.");
                return 1;
            }

            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), name)))
            {
                Logger.Log(Logger.LogSeverity.Error, $"Directory '{name}' already exists.");
                return 1;
            }

            ProjectScaffolder.Scaffold(name);
            return 0;
        }

        private static bool IsValidProjectName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z0-9_]*$");
        }
    }
}
