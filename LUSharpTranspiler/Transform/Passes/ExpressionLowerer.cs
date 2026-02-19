using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Expressions;
using LUSharpTranspiler.Transform.IR.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transform.Passes;

public class ExpressionLowerer
{
    private readonly TypeResolver _types;

    public ExpressionLowerer(TypeResolver types) => _types = types;

    public ILuaExpression Lower(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit                               => LowerLiteral(lit),
        IdentifierNameSyntax id                                   => new LuaIdent(id.Identifier.Text),
        InterpolatedStringExpressionSyntax i                      => LowerInterpolated(i),
        BinaryExpressionSyntax bin                                => LowerBinary(bin),
        PrefixUnaryExpressionSyntax pre                           => LowerPrefixUnary(pre),
        PostfixUnaryExpressionSyntax post                         => LowerPostfixUnary(post),
        MemberAccessExpressionSyntax mem                          => LowerMemberAccess(mem),
        InvocationExpressionSyntax inv                            => LowerInvocation(inv),
        ObjectCreationExpressionSyntax oc                         => LowerObjectCreation(oc),
        ImplicitObjectCreationExpressionSyntax ic                 => LowerImplicitCreation(ic),
        ConditionalExpressionSyntax cond                          => LowerTernary(cond),
        ConditionalAccessExpressionSyntax ca                      => LowerNullSafe(ca),
        ElementAccessExpressionSyntax el                          => LowerIndex(el),
        AssignmentExpressionSyntax assign                         => LowerAssignmentExpr(assign),
        ParenthesizedLambdaExpressionSyntax lam                   => LowerLambda(lam.ParameterList, lam.Body),
        SimpleLambdaExpressionSyntax lam                          => LowerSimpleLambda(lam),
        ParenthesizedExpressionSyntax par                         => new LuaParen(Lower(par.Expression)),
        CastExpressionSyntax cast                                 => Lower(cast.Expression), // strip casts
        _                                                         => new LuaLiteral($"--[[unsupported: {expr.Kind()}]]")
    };

    private static LuaLiteral LowerLiteral(LiteralExpressionSyntax lit) => lit.Kind() switch
    {
        SyntaxKind.StringLiteralExpression  => new LuaLiteral($"\"{EscapeString(lit.Token.ValueText)}\""),
        SyntaxKind.NumericLiteralExpression => new LuaLiteral(lit.Token.Text),
        SyntaxKind.TrueLiteralExpression    => LuaLiteral.True,
        SyntaxKind.FalseLiteralExpression   => LuaLiteral.False,
        SyntaxKind.NullLiteralExpression    => LuaLiteral.Nil,
        _                                   => new LuaLiteral(lit.Token.Text)
    };

    private static LuaInterp LowerInterpolated(InterpolatedStringExpressionSyntax interp)
    {
        var sb = new System.Text.StringBuilder("`");
        foreach (var content in interp.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
                sb.Append(text.TextToken.ValueText);
            else if (content is InterpolationSyntax hole)
                sb.Append($"{{{hole.Expression}}}");
        }
        sb.Append('`');
        return new LuaInterp(sb.ToString());
    }

    private ILuaExpression LowerBinary(BinaryExpressionSyntax bin)
    {
        // Fix #2: `as` and `is` casts — strip to just the left operand
        if (bin.IsKind(SyntaxKind.AsExpression) || bin.IsKind(SyntaxKind.IsExpression))
            return Lower(bin.Left);

        // Fix #7: Removed FlattenConcat — without type info we can't distinguish
        // string + from numeric +. Keep + as + always; use $"" for string concat.

        var left = Lower(bin.Left);
        var right = Lower(bin.Right);

        var op = bin.Kind() switch
        {
            SyntaxKind.AddExpression                  => "+",
            SyntaxKind.SubtractExpression             => "-",
            SyntaxKind.MultiplyExpression             => "*",
            SyntaxKind.DivideExpression               => "/",
            SyntaxKind.ModuloExpression               => "%",
            SyntaxKind.EqualsExpression               => "==",
            SyntaxKind.NotEqualsExpression            => "~=",
            SyntaxKind.LessThanExpression             => "<",
            SyntaxKind.LessThanOrEqualExpression      => "<=",
            SyntaxKind.GreaterThanExpression          => ">",
            SyntaxKind.GreaterThanOrEqualExpression   => ">=",
            SyntaxKind.LogicalAndExpression           => "and",
            SyntaxKind.LogicalOrExpression            => "or",
            SyntaxKind.CoalesceExpression             => null, // handled below
            _                                         => bin.OperatorToken.Text
        };

        if (op == null) return LowerCoalesce(bin);
        return new LuaBinary(left, op, right);
    }

    private ILuaExpression LowerPrefixUnary(PrefixUnaryExpressionSyntax pre)
    {
        var op = pre.Kind() switch
        {
            SyntaxKind.LogicalNotExpression  => "not",
            SyntaxKind.UnaryMinusExpression  => "-",
            SyntaxKind.PreIncrementExpression => null, // x++ → handled as assign
            _                                => pre.OperatorToken.Text
        };
        if (op == null) return Lower(pre.Operand); // fallback
        return new LuaUnary(op, Lower(pre.Operand));
    }

    private ILuaExpression LowerPostfixUnary(PostfixUnaryExpressionSyntax post) =>
        Lower(post.Operand); // i++ treated as expression value (statement handles increment)

    private ILuaExpression LowerMemberAccess(MemberAccessExpressionSyntax mem)
    {
        var memberName = mem.Name.Identifier.Text;

        // Fix #4: Roblox enum prefix — Material.Neon → Enum.Material.Neon
        if (mem.Expression is IdentifierNameSyntax enumId && RobloxEnumNames.Contains(enumId.Identifier.Text))
            return new LuaMember(new LuaMember(new LuaIdent("Enum"), enumId.Identifier.Text), memberName);

        // Fix #6 (partial): list.Count → #list
        if (memberName == "Count" || memberName == "Length")
            return new LuaUnary("#", Lower(mem.Expression));

        return new LuaMember(Lower(mem.Expression), memberName);
    }

    private ILuaExpression LowerInvocation(InvocationExpressionSyntax inv)
    {
        var args = inv.ArgumentList.Arguments
            .Select(a => Lower(a.Expression)).ToList();

        if (inv.Expression is MemberAccessExpressionSyntax mem)
        {
            var obj = mem.Expression.ToString();
            var method = mem.Name.Identifier.Text;

            // Extract generic type argument if present (Fix #1)
            string? genericTypeArg = null;
            if (mem.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
            {
                genericTypeArg = generic.TypeArgumentList.Arguments[0].ToString();
                method = generic.Identifier.Text;
            }

            // Map Console.WriteLine → print
            if (obj == "Console" && method == "WriteLine")
                return new LuaCall(new LuaIdent("print"), args);

            // Fix #3: Math.Min/Max/etc → math.min/max/etc (dot call, not colon)
            if (obj == "Math")
                return new LuaCall(new LuaMember(new LuaIdent("math"), method.ToLower()), args);

            // Fix #5: Instance.New<T>(...) → Instance.new("T", ...)
            if (obj == "Instance" && method == "New" && genericTypeArg != null)
            {
                var newArgs = new List<ILuaExpression> { LuaLiteral.FromString(genericTypeArg) };
                newArgs.AddRange(args);
                return new LuaCall(new LuaMember(new LuaIdent("Instance"), "new"), newArgs);
            }

            // Fix #1: Generic method calls — GetService<Players>() → GetService("Players")
            if (genericTypeArg != null)
            {
                args.Insert(0, LuaLiteral.FromString(genericTypeArg));
            }

            // Fix #6: Collection method rewrites (expression-level)
            // dict.ContainsKey(key) → dict[key] ~= nil
            if (method == "ContainsKey" && args.Count == 1)
                return new LuaBinary(new LuaIndex(Lower(mem.Expression), args[0]), "~=", LuaLiteral.Nil);

            // list.Contains(item) → table.find(list, item) ~= nil
            if (method == "Contains" && args.Count == 1)
            {
                var findCall = new LuaCall(
                    new LuaMember(new LuaIdent("table"), "find"),
                    new List<ILuaExpression> { Lower(mem.Expression), args[0] });
                return new LuaBinary(findCall, "~=", LuaLiteral.Nil);
            }

            // list.Add(item) → table.insert(list, item)
            if (method == "Add" && args.Count == 1)
                return new LuaCall(
                    new LuaMember(new LuaIdent("table"), "insert"),
                    new List<ILuaExpression> { Lower(mem.Expression), args[0] });

            // list.IndexOf(item) → table.find(list, item)
            if (method == "IndexOf" && args.Count == 1)
                return new LuaCall(
                    new LuaMember(new LuaIdent("table"), "find"),
                    new List<ILuaExpression> { Lower(mem.Expression), args[0] });

            // string.ToString() → tostring(x)
            if (method == "ToString" && args.Count == 0)
                return new LuaCall(new LuaIdent("tostring"), new List<ILuaExpression> { Lower(mem.Expression) });

            return new LuaMethodCall(Lower(mem.Expression), method, args);
        }

        return new LuaCall(Lower(inv.Expression), args);
    }

    private ILuaExpression LowerObjectCreation(ObjectCreationExpressionSyntax oc)
    {
        var className = oc.Type.ToString();
        var args = oc.ArgumentList?.Arguments
            .Select(a => Lower(a.Expression)).ToList() ?? new();
        return new LuaNew(className, args);
    }

    private ILuaExpression LowerImplicitCreation(ImplicitObjectCreationExpressionSyntax ic)
    {
        // new() { ... } — typically a collection initializer
        if (ic.Initializer != null)
            return LowerInitializer(ic.Initializer);
        return LuaTable.Empty;
    }

    private LuaTable LowerInitializer(InitializerExpressionSyntax init)
    {
        var entries = new List<LuaTableEntry>();
        foreach (var expr in init.Expressions)
        {
            if (expr is InitializerExpressionSyntax kv && kv.Expressions.Count == 2)
            {
                var k = kv.Expressions[0].ToString().Trim('"');
                var v = Lower(kv.Expressions[1]);
                entries.Add(new LuaTableEntry(k, v));
            }
            else
            {
                entries.Add(new LuaTableEntry(null, Lower(expr)));
            }
        }
        return new LuaTable(entries);
    }

    private ILuaExpression LowerTernary(ConditionalExpressionSyntax c) =>
        new LuaTernary(Lower(c.Condition), Lower(c.WhenTrue), Lower(c.WhenFalse));

    private ILuaExpression LowerNullSafe(ConditionalAccessExpressionSyntax ca)
    {
        var member = ca.WhenNotNull is MemberBindingExpressionSyntax mb
            ? mb.Name.Identifier.Text : ca.WhenNotNull.ToString();
        return new LuaNullSafe(Lower(ca.Expression), member);
    }

    private ILuaExpression LowerCoalesce(BinaryExpressionSyntax bin) =>
        new LuaCoalesce(Lower(bin.Left), Lower(bin.Right));

    private ILuaExpression LowerIndex(ElementAccessExpressionSyntax el)
    {
        var key = Lower(el.ArgumentList.Arguments.First().Expression);
        return new LuaIndex(Lower(el.Expression), key);
    }

    private ILuaExpression LowerAssignmentExpr(AssignmentExpressionSyntax assign) =>
        Lower(assign.Right); // assignment-as-expression; statement lowerer handles the assign

    private ILuaExpression LowerLambda(ParameterListSyntax paramList, CSharpSyntaxNode body)
    {
        var parms = paramList.Parameters.Select(p => p.Identifier.Text).ToList();
        var stmts = body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LuaReturn(Lower((ExpressionSyntax)body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private ILuaExpression LowerSimpleLambda(SimpleLambdaExpressionSyntax lam)
    {
        var parms = new List<string> { lam.Parameter.Identifier.Text };
        var stmts = lam.Body is BlockSyntax block
            ? StatementLowerer.LowerBlock(block.Statements, this)
            : new List<ILuaStatement> { new LuaReturn(Lower((ExpressionSyntax)lam.Body)) };
        return new LuaLambda { Parameters = parms, Body = stmts };
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Known Roblox enum type names — sourced from LUSharpAPI/Runtime/STL/Enums/ and Generated/Enums/
    private static readonly HashSet<string> RobloxEnumNames = new()
    {
        // Hand-crafted STL enums
        "AssetType", "AvatarItemType", "Axis", "BulkMoveMode", "CameraMode",
        "CollisionFidelity", "ContentSourceType", "CreatorType", "DevCameraOcclusionMode",
        "DevComputerCameraMovementMode", "DevComputerMovementMode", "DevTouchCameraMovementMode",
        "DevTouchMovementMode", "IKCollisionsMode", "JoinSource", "MatchmakingType", "Material",
        "MembershipType", "ModelLevelOfDetail", "ModelStreamingMode", "NormalId", "PartType",
        "PlayerExitReason", "RaycastFilterType", "RenderFidelity", "RotationOrder", "RunContext",
        "SecurityCapability", "SurfaceType",
        // Commonly used generated enums
        "EasingDirection", "EasingStyle", "Font", "FontSize", "FontStyle", "FontWeight",
        "HumanoidRigType", "HumanoidStateType", "KeyCode", "UserInputState", "UserInputType",
        "AnimationPriority", "CameraType", "FormFactor", "SortOrder", "FillDirection",
        "ScaleType", "SizeConstraint", "TextXAlignment", "TextYAlignment", "BorderMode",
        "ElasticBehavior", "ScrollingDirection", "AutomaticSize", "HorizontalAlignment",
        "VerticalAlignment", "TextTruncate", "ZIndexBehavior", "PlaybackState", "ScreenInsets",
        "CoreGuiType", "HttpContentType", "HttpError", "TweenStatus", "PathStatus",
        "PathWaypointAction", "RollOffMode", "ReverbType", "DialogPurpose", "DialogTone",
        "DialogBehaviorType", "ExplosionType", "MeshType", "RenderPriority", "Technology",
        "ParticleEmitterShape", "ParticleOrientation", "ParticleFlipbookMode", "Style",
        "MouseBehavior", "ScreenOrientation", "StartCorner", "ProximityPromptStyle",
        "ProximityPromptExclusivity", "DragDetectorDragStyle", "DragDetectorResponseStyle",
        "AlignType", "ActuatorType", "HighlightDepthMode", "StudioStyleGuideColor",
        "ListenerType", "SurfaceGuiSizingMode", "SafeAreaCompatibility",
    };
}
