using Microsoft.CodeAnalysis;

namespace LUSharpTranspiler.Transform;

public record ParsedFile(string FilePath, SyntaxTree Tree);
