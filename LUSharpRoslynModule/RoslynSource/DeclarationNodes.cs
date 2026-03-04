namespace RoslynLuau;

public class ParameterSyntax
{
    public string TypeName { get; }
    public string Name { get; }

    public ParameterSyntax(string typeName, string name)
    {
        TypeName = typeName;
        Name = name;
    }

    public string ToDisplayString()
    {
        return TypeName + " " + Name;
    }
}

public class MethodDeclarationSyntax : MemberDeclarationSyntax
{
    public string ReturnType { get; }
    public string Name { get; }
    public ParameterSyntax[] Parameters { get; }
    public BlockSyntax Body { get; }
    public bool IsStatic { get; }

    public MethodDeclarationSyntax(string returnType, string name, ParameterSyntax[] parameters, BlockSyntax body, bool isStatic) : base(8875)
    {
        ReturnType = returnType;
        Name = name;
        Parameters = parameters;
        Body = body;
        IsStatic = isStatic;
    }

    public override string Accept()
    {
        string mods = "";
        if (IsStatic) mods = "static ";
        string parms = "";
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) parms = parms + ", ";
            parms = parms + Parameters[i].ToDisplayString();
        }
        return mods + ReturnType + " " + Name + "(" + parms + ") " + Body.Accept();
    }

    public override string ToDisplayString()
    {
        return "Method(" + Name + ")";
    }
}

public class FieldDeclarationSyntax : MemberDeclarationSyntax
{
    public string TypeName { get; }
    public string Name { get; }
    public ExpressionSyntax Initializer { get; }
    public bool IsStatic { get; }

    public FieldDeclarationSyntax(string typeName, string name, ExpressionSyntax initializer, bool isStatic) : base(8873)
    {
        TypeName = typeName;
        Name = name;
        Initializer = initializer;
        IsStatic = isStatic;
    }

    public override string Accept()
    {
        string mods = "";
        if (IsStatic) mods = "static ";
        if (Initializer == null)
            return mods + TypeName + " " + Name + ";";
        return mods + TypeName + " " + Name + " = " + Initializer.Accept() + ";";
    }

    public override string ToDisplayString()
    {
        return "Field(" + Name + ")";
    }
}

public class ClassDeclarationSyntax : MemberDeclarationSyntax
{
    public string Name { get; }
    public MemberDeclarationSyntax[] Members { get; }

    public ClassDeclarationSyntax(string name, MemberDeclarationSyntax[] members) : base(8855)
    {
        Name = name;
        Members = members;
    }

    public override string Accept()
    {
        string result = "class " + Name + " {\n";
        for (int i = 0; i < Members.Length; i++)
        {
            result = result + "  " + Members[i].Accept() + "\n";
        }
        result = result + "}";
        return result;
    }

    public override string ToDisplayString()
    {
        return "Class(" + Name + ", " + Members.Length + " members)";
    }
}

public class CompilationUnitSyntax : SyntaxNode
{
    public MemberDeclarationSyntax[] Members { get; }

    public CompilationUnitSyntax(MemberDeclarationSyntax[] members) : base(8840)
    {
        Members = members;
    }

    public override string Accept()
    {
        string result = "";
        for (int i = 0; i < Members.Length; i++)
        {
            result = result + Members[i].Accept() + "\n";
        }
        return result;
    }

    public override string ToDisplayString()
    {
        return "CompilationUnit(" + Members.Length + " members)";
    }
}
