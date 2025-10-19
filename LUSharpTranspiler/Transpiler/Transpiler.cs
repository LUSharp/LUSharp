using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transpiler
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
            // Get all .cs files in the project directory and its subdirectories
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles)
            {
                // Read the content of the file
                var code = File.ReadAllText(file);
                // Parse the code into a syntax tree
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                // verify the file has no errors
                if(syntaxTree.GetCompilationUnitRoot().ContainsDiagnostics)
                {
                    Logger.Log(LogSeverity.Warning, $"The file {file} contains syntax errors. Skipping...");

                    // print the diagnostics(Errors)
                    var diagnostics = syntaxTree.GetDiagnostics();
                    foreach(var diag in diagnostics)
                    {
                        Logger.Log((LogSeverity)(diag.Severity), $"{diag.ToString()} in file /{file.Substring(file.LastIndexOf("\\") + 1)}");
                    }

                    continue;
                }

                ParseSyntaxTree(syntaxTree);
            }
            Logger.Log(LogSeverity.Info, "Transpilation completed.");
        }
        /// <summary>
        /// Reads
        /// </summary>
        /// <param name="syntaxTree"></param>
        private static void ParseSyntaxTree(SyntaxTree syntaxTree)
        {
            // For now lets just print the root node kind and try to understand the structure of everything
            var root = syntaxTree.GetRoot();

            // validate the root node is a compilation unit
            if(root.Kind() != SyntaxKind.CompilationUnit)
            {
                Logger.Log(LogSeverity.Error, "The syntax tree root is not a compilation unit.");
                return;
            }

            if (!VerifyEntryStructure(root))
                return;

            CodeWalker roCodeBuilder = new CodeWalker();
            roCodeBuilder.Visit(root);
            var luauCode = roCodeBuilder.GetFinalizedCode();
            Console.WriteLine("Generated Luau Code:");
            Console.WriteLine(luauCode);
        }


        private static bool VerifyEntryStructure(SyntaxNode tree)
        {
            if(tree.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(x => x.Identifier.Text == "Main"))
            {
                if (tree.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(x => x.Identifier.Text == "GameEntry"))
                {
                    return true;
                }

                Logger.Log(LogSeverity.Warning, "Project doesnt contain entry method named 'GameEntry'.");
                return false;
            }

            Logger.Log(LogSeverity.Warning, "Project doesnt contain entry class named 'Main'.");
            return false;
        }

        private static void PrintNode(SyntaxNode node, int indent = 0)
        {
            Console.WriteLine(new string(' ', indent * 2) + node.Kind());
            foreach (var child in node.ChildNodes())
            {
                PrintNode(child, indent + 1);
            }
        }
    }
}
