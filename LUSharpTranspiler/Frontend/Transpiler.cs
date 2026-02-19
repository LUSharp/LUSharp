using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using LUSharpTranspiler.Transform;

namespace LUSharpTranspiler.Frontend
{
    /// <summary>
    /// Heart class for transpiling C# projects.
    /// </summary>
    internal class Transpiler
    {
        /// <summary>
        /// Initiate transpilation of the project
        /// </summary>
        /// <param name="projectPath">Input project directory.</param>
        /// <param name="outputPath">Output project directory.</param>
        public static void TranspileProject(string projectPath, string outputPath)
        {
            var allFiles = new List<ParsedFile>();

            foreach (var file in GetAllSourceFiles(projectPath))
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);

                if (tree.GetCompilationUnitRoot().ContainsDiagnostics)
                {
                    ReportDiagnostics(file, tree);
                    continue;
                }

                allFiles.Add(new ParsedFile(file, tree));
            }

            var pipeline = new TransformPipeline();
            var modules = pipeline.Run(allFiles);

            // Backend emission wired in later
            foreach (var m in modules)
                Logger.Log(LogSeverity.Info, $"Processed: {m.SourceFile} → {m.OutputPath}");

            Logger.Log(LogSeverity.Info, $"Transpilation complete. {modules.Count} modules.");
        }

        private static IEnumerable<string> GetAllSourceFiles(string projectPath)
        {
            var patterns = new[] { "Client", "Server", "Shared" };
            return patterns.SelectMany(p =>
                Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                         .Where(f => f.Contains(p)));
        }

        private static void ReportDiagnostics(string file, SyntaxTree tree)
        {
            Logger.Log(LogSeverity.Warning, $"{file} contains syntax errors. Skipping...");
            foreach (var d in tree.GetDiagnostics())
                Logger.Log(d.Severity, d.ToString());
        }
    }
}
