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

public class EnumMemberSyntax
{
    public string Name { get; }
    public ExpressionSyntax Value { get; }

    public EnumMemberSyntax(string name, ExpressionSyntax value)
    {
        Name = name;
        Value = value;
    }

    public string ToDisplayString()
    {
        if (Value == null)
            return Name;
        return Name + " = " + Value.Accept();
    }
}

public class EnumDeclarationSyntax : MemberDeclarationSyntax
{
    public string Name { get; }
    public EnumMemberSyntax[] Members { get; }

    public EnumDeclarationSyntax(string name, EnumMemberSyntax[] members) : base(8856)
    {
        Name = name;
        Members = members;
    }

    public override string Accept()
    {
        string result = "enum " + Name + " {\n";
        for (int i = 0; i < Members.Length; i++)
        {
            if (i > 0) result = result + ",\n";
            result = result + "  " + Members[i].ToDisplayString();
        }
        result = result + "\n}";
        return result;
    }

    public override string ToDisplayString()
    {
        return "Enum(" + Name + ", " + Members.Length + " members)";
    }
}

public class StructDeclarationSyntax : MemberDeclarationSyntax
{
    public string Name { get; }
    public MemberDeclarationSyntax[] Members { get; }

    public StructDeclarationSyntax(string name, MemberDeclarationSyntax[] members) : base(8857)
    {
        Name = name;
        Members = members;
    }

    public override string Accept()
    {
        string result = "struct " + Name + " {\n";
        for (int i = 0; i < Members.Length; i++)
        {
            result = result + "  " + Members[i].Accept() + "\n";
        }
        result = result + "}";
        return result;
    }

    public override string ToDisplayString()
    {
        return "Struct(" + Name + ", " + Members.Length + " members)";
    }
}

public class PropertyDeclarationSyntax : MemberDeclarationSyntax
{
    public string TypeName { get; }
    public string Name { get; }
    public bool HasGetter { get; }
    public bool HasSetter { get; }
    public ExpressionSyntax ExpressionBody { get; }
    public bool IsStatic { get; }

    public PropertyDeclarationSyntax(string typeName, string name, bool hasGetter, bool hasSetter, ExpressionSyntax expressionBody, bool isStatic) : base(8892)
    {
        TypeName = typeName;
        Name = name;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        ExpressionBody = expressionBody;
        IsStatic = isStatic;
    }

    public override string Accept()
    {
        string mods = "";
        if (IsStatic) mods = "static ";
        string result = mods + TypeName + " " + Name;
        if (ExpressionBody != null)
        {
            result = result + " => " + ExpressionBody.Accept() + ";";
        }
        else
        {
            result = result + " { ";
            if (HasGetter) result = result + "get; ";
            if (HasSetter) result = result + "set; ";
            result = result + "}";
        }
        return result;
    }

    public override string ToDisplayString()
    {
        return "Property(" + Name + ")";
    }
}

public class ConstructorDeclarationSyntax : MemberDeclarationSyntax
{
    public string Name { get; }
    public ParameterSyntax[] Parameters { get; }
    public BlockSyntax Body { get; }
    public string BaseOrThisKeyword { get; }
    public ExpressionSyntax[] InitializerArguments { get; }

    public ConstructorDeclarationSyntax(string name, ParameterSyntax[] parameters, BlockSyntax body, string baseOrThisKeyword, ExpressionSyntax[] initializerArguments) : base(8878)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
        BaseOrThisKeyword = baseOrThisKeyword;
        InitializerArguments = initializerArguments;
    }

    public override string Accept()
    {
        string parms = "";
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) parms = parms + ", ";
            parms = parms + Parameters[i].ToDisplayString();
        }
        string result = Name + "(" + parms + ")";
        if (BaseOrThisKeyword != null)
        {
            result = result + " : " + BaseOrThisKeyword + "(";
            for (int i = 0; i < InitializerArguments.Length; i++)
            {
                if (i > 0) result = result + ", ";
                result = result + InitializerArguments[i].Accept();
            }
            result = result + ")";
        }
        if (Body != null) result = result + " " + Body.Accept();
        return result;
    }

    public override string ToDisplayString()
    {
        return "Constructor(" + Name + ")";
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
