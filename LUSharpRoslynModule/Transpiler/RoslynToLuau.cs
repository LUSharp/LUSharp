using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpRoslynModule.Transpiler;

public class RoslynToLuau
{
    public TranspileResult Transpile(string sourceCode, string fileName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: fileName);
        var root = tree.GetCompilationUnitRoot();

        var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (diagnostics.Count > 0)
        {
            return new TranspileResult
            {
                Success = false,
                FileName = fileName,
                Errors = diagnostics.Select(d => d.ToString()).ToList()
            };
        }

        var emitter = new LuauEmitter();
        emitter.EmitHeader();

        foreach (var member in root.Members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case FileScopedNamespaceDeclarationSyntax ns:
                    EmitNamespaceMembers(ns.Members, emitter);
                    break;
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                case ClassDeclarationSyntax classDecl:
                    emitter.EmitClass(classDecl);
                    break;
                default:
                    Console.Error.WriteLine($"Warning: unsupported top-level declaration: {member.Kind()}");
                    break;
            }
        }

        // Post-process: insert require() statements for referenced modules
        InsertRequires(emitter);

        return new TranspileResult
        {
            Success = true,
            FileName = fileName,
            LuauSource = emitter.GetOutput()
        };
    }

    private void EmitNamespaceMembers(SyntaxList<MemberDeclarationSyntax> members, LuauEmitter emitter)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case EnumDeclarationSyntax enumDecl:
                    emitter.EmitEnum(enumDecl);
                    break;
                case ClassDeclarationSyntax classDecl:
                    emitter.EmitClass(classDecl);
                    break;
                default:
                    Console.Error.WriteLine($"Warning: unsupported member: {member.Kind()}");
                    break;
            }
        }
    }

    /// <summary>
    /// After emission, insert require() statements for any external modules referenced.
    /// Uses the pattern:
    ///   local _ModuleName = require(script.Parent.ModuleName)
    ///   local ModuleName = _ModuleName.ModuleName
    /// </summary>
    private void InsertRequires(LuauEmitter emitter)
    {
        if (emitter.ReferencedModules.Count == 0) return;

        // Filter out modules that are known not to need requires
        var modulesToRequire = emitter.ReferencedModules
            .Where(m => !IsIgnoredModule(m))
            .OrderBy(m => m)
            .ToList();

        if (modulesToRequire.Count == 0) return;

        var requireBlock = new System.Text.StringBuilder();
        foreach (var module in modulesToRequire)
        {
            requireBlock.AppendLine($"local _{module} = require(script.Parent.{module})");
            requireBlock.AppendLine($"local {module} = _{module}.{module}");
        }
        requireBlock.AppendLine();

        emitter.InsertAfterHeader(requireBlock.ToString());
    }

    /// <summary>
    /// Modules that should not get a require() statement
    /// (e.g., CharUnicodeInfo — external .NET, not a Luau module).
    /// </summary>
    private static bool IsIgnoredModule(string module)
    {
        return module is "CharUnicodeInfo" or "UnicodeCategory"
            or "ArgumentOutOfRangeException" or "Math" or "Console"
            or "Char" or "String" or "Int32" or "Convert";
    }
}

public class TranspileResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = "";
    public string LuauSource { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}
