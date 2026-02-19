using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class MethodBodyLowerer
{
    private readonly TypeResolver _types;
    private readonly ExpressionLowerer _exprs;

    public MethodBodyLowerer(SymbolTable symbols)
    {
        _types = new TypeResolver(symbols);
        _exprs = new ExpressionLowerer(_types);
    }

    public LuaClassDef Lower(ClassDeclarationSyntax cls)
    {
        var def = new LuaClassDef { Name = cls.Identifier.Text };

        // First pass: collect fields and properties to know instance member names
        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    LowerField(field, def);
                    break;
                case PropertyDeclarationSyntax prop:
                    LowerProperty(prop, def);
                    break;
            }
        }

        // Collect instance member names for implicit this→self rewriting
        var instanceMembers = new HashSet<string>(
            def.InstanceFields.Select(f => f.Name));

        // Second pass: lower methods and constructors (needs instance member names)
        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method when method.Body != null:
                    def.Methods.Add(LowerMethod(method, instanceMembers));
                    break;
                case ConstructorDeclarationSyntax ctor when ctor.Body != null:
                    def.Constructor = LowerConstructor(ctor, cls.Identifier.Text, instanceMembers);
                    break;
            }
        }

        return def;
    }

    private LuaMethodDef LowerMethod(MethodDeclarationSyntax method, HashSet<string> instanceMembers)
    {
        var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        var parms = new List<string>();
        if (!isStatic) parms.Add("self");
        parms.AddRange(method.ParameterList.Parameters.Select(p => p.Identifier.Text));

        var body = StatementLowerer.LowerBlock(method.Body!.Statements, _exprs);

        // Rewrite this.X → self.X and bare MemberName → self.MemberName in instance methods
        if (!isStatic)
            body = IrRewriter.RewriteBlock(body, instanceMembers);

        return new LuaMethodDef
        {
            Name = method.Identifier.Text,
            IsStatic = isStatic,
            Parameters = parms,
            Body = body
        };
    }

    private LuaMethodDef LowerConstructor(ConstructorDeclarationSyntax ctor, string className, HashSet<string> instanceMembers)
    {
        var parms = ctor.ParameterList.Parameters.Select(p => p.Identifier.Text).ToList();
        var body = new List<ILuaStatement>();

        // local self = {}
        body.Add(new LuaLocal("self", LuaTable.Empty));

        // Body statements (this.X / bare X → self.X)
        body.AddRange(StatementLowerer.LowerBlock(ctor.Body!.Statements, _exprs)
            .Select(s => IrRewriter.RewriteThisToSelf(s, instanceMembers)));

        // return self
        body.Add(new LuaReturn(new LuaIdent("self")));

        return new LuaMethodDef
        {
            Name = "new",
            IsStatic = true,
            Parameters = parms,
            Body = body
        };
    }

    private void LowerField(FieldDeclarationSyntax field, LuaClassDef def)
    {
        var isStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        foreach (var v in field.Declaration.Variables)
        {
            var value = v.Initializer != null ? _exprs.Lower(v.Initializer.Value) : null;
            var fd = new LuaFieldDef(v.Identifier.Text, value, isStatic);
            if (isStatic) def.StaticFields.Add(fd);
            else def.InstanceFields.Add(fd);
        }
    }

    private void LowerProperty(PropertyDeclarationSyntax prop, LuaClassDef def)
    {
        var isStatic = prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        var value = prop.Initializer != null ? _exprs.Lower(prop.Initializer.Value) : null;
        var fd = new LuaFieldDef(prop.Identifier.Text, value, isStatic);
        if (isStatic) def.StaticFields.Add(fd);
        else def.InstanceFields.Add(fd);

        // Auto-generate getter/setter methods
        if (prop.AccessorList != null)
        {
            if (prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
                def.Methods.Add(MakeGetter(prop.Identifier.Text, isStatic));
            if (prop.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
                def.Methods.Add(MakeSetter(prop.Identifier.Text, isStatic));
        }
    }

    private static LuaMethodDef MakeGetter(string name, bool isStatic) => new()
    {
        Name = $"get_{name}",
        IsStatic = isStatic,
        Parameters = isStatic ? new() : new() { "self" },
        Body = new() { new LuaReturn(new LuaMember(new LuaIdent(isStatic ? name : "self"), name)) }
    };

    private static LuaMethodDef MakeSetter(string name, bool isStatic) => new()
    {
        Name = $"set_{name}",
        IsStatic = isStatic,
        Parameters = isStatic ? new() { "value" } : new() { "self", "value" },
        Body = new() { new LuaAssign(new LuaMember(new LuaIdent(isStatic ? name : "self"), name), new LuaIdent("value")) }
    };

}
