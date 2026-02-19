using LUSharpTranspiler.Transform.IR;

namespace LUSharpTranspiler.Transform;

public record ClassSymbol(
    string Name,
    string SourceFile,
    ScriptType ScriptType,
    string BaseClass  // "ModuleScript", "LocalScript", "Script", "RobloxScript"
);
