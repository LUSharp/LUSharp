using LUSharp.Project.Rojo;

namespace LUSharp
{
    internal class Program
    {
        private static Dictionary<string, (string, string)> commands = new()
        {
            {"help", ("command_name(optional)", "List usage for all or a specific command.")},
            {"new", ("project_name[REQUIRED]", "Create a new project with the name project_name.") }
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
            // check if the project exists already in the current directory as the process. 
            if (Directory.Exists(Directory.GetCurrentDirectory() + $"\\{name}"))
            {
                Logger.Log(Logger.LogSeverity.Error, "Project in directory exists.");
                return;
            }
            else
            {
                // if the directory doesnt exist
                var projectDir = Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"\\{name}");

                // do project init and rojo
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

                            // ===== SERVER OUTPUT =====
                            ["ServerScriptService"] = new RojoInstance
                            {
                                ClassName = "ServerScriptService",
                                Path = "out/server"
                            },

                            // ===== SHARED OUTPUT =====
                            ["ReplicatedStorage"] = new RojoInstance
                            {
                                ClassName = "ReplicatedStorage",
                                Children = new Dictionary<string, RojoInstance>
                                {
                                    ["Shared"] = new RojoInstance
                                    {
                                        Path = "out/shared"
                                    },

                                    ["Runtime"] = new RojoInstance
                                    {
                                        Path = "out/runtime"
                                    }
                                }
                            },

                            // ===== CLIENT OUTPUT =====
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


                // Write ROJO project to the project directory
                RojoManager.WriteConfig($"{projectDir}\\default.project.json", config);

                /*== Code Directories ==*/
                Directory.CreateDirectory($"{projectDir}\\client");
                Directory.CreateDirectory($"{projectDir}\\server");
                Directory.CreateDirectory($"{projectDir}\\shared");
                /*== OUTPUT DIRECTORIES ==*/
                Directory.CreateDirectory($"{projectDir}\\out");
                Directory.CreateDirectory($"{projectDir}\\out\\client");
                Directory.CreateDirectory($"{projectDir}\\out\\server");
                Directory.CreateDirectory($"{projectDir}\\out\\shared");
                Directory.CreateDirectory($"{projectDir}\\out\\runtime");

                Logger.Log("Project created successfully.");

                // VS Project Generation/Implementation maybe?
            }
        }
    }
}
