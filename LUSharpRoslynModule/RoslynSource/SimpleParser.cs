using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

namespace RoslynLuau;

/// <summary>
/// Simplified recursive descent parser for C# source code.
/// Produces a syntax tree using the SyntaxNode hierarchy.
/// </summary>
public class SimpleParser
{
    private SimpleTokenizer _tokenizer;
    private SimpleTokenizer.TokenInfo[] _tokens;
    private int _position;
    private string _currentClassName;

    public SimpleParser(string text)
    {
        _tokenizer = new SimpleTokenizer(text);
        var tokenList = _tokenizer.Tokenize();
        // Filter out trivia (whitespace, newlines, comments)
        var filtered = new SimpleTokenizer.TokenInfo[tokenList.Count];
        int count = 0;
        for (int i = 0; i < tokenList.Count; i++)
        {
            var t = tokenList[i];
            if (t.Kind != SyntaxKind.WhitespaceTrivia
                && t.Kind != SyntaxKind.EndOfLineTrivia
                && t.Kind != SyntaxKind.SingleLineCommentTrivia)
            {
                filtered[count] = t;
                count++;
            }
        }
        _tokens = new SimpleTokenizer.TokenInfo[count];
        for (int i = 0; i < count; i++)
        {
            _tokens[i] = filtered[i];
        }
        _position = 0;
    }

    private SimpleTokenizer.TokenInfo Current()
    {
        if (_position >= _tokens.Length)
            return new SimpleTokenizer.TokenInfo(SyntaxKind.EndOfFileToken, "", 0, 0);
        return _tokens[_position];
    }

    private SimpleTokenizer.TokenInfo Advance()
    {
        var token = Current();
        _position++;
        return token;
    }

    private SimpleTokenizer.TokenInfo Expect(SyntaxKind kind)
    {
        var token = Current();
        if (token.Kind != kind)
        {
            // Error recovery: return a missing token
            return new SimpleTokenizer.TokenInfo(kind, "", token.Start, 0);
        }
        return Advance();
    }

    private bool IsAtEnd()
    {
        return _position >= _tokens.Length || Current().Kind == SyntaxKind.EndOfFileToken;
    }

    private SyntaxToken MakeSyntaxToken(SimpleTokenizer.TokenInfo info)
    {
        return new SyntaxToken((int)info.Kind, info.Text, info.Start, info.Length);
    }

    // === Top-Level Parsing ===

    public CompilationUnitSyntax ParseCompilationUnit()
    {
        var members = new MemberDeclarationSyntax[256];
        int count = 0;

        // Skip 'using' directives
        while (!IsAtEnd() && Current().Kind == SyntaxKind.UsingKeyword)
        {
            SkipUsingDirective();
        }

        // Skip 'namespace' declaration (simplified: just skip to the opening brace or the members)
        if (!IsAtEnd() && Current().Kind == SyntaxKind.NamespaceKeyword)
        {
            SkipNamespaceHeader();
        }

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var member = ParseMemberDeclaration();
            if (member != null && count < 256)
            {
                members[count] = member;
                count++;
            }
        }

        // Skip closing brace of namespace if present
        if (!IsAtEnd() && Current().Kind == SyntaxKind.CloseBraceToken)
            Advance();

        var result = new MemberDeclarationSyntax[count];
        for (int i = 0; i < count; i++)
            result[i] = members[i];

        return new CompilationUnitSyntax(result);
    }

    private void SkipUsingDirective()
    {
        while (!IsAtEnd() && Current().Kind != SyntaxKind.SemicolonToken)
            Advance();
        if (!IsAtEnd()) Advance(); // skip ;
    }

    private void SkipNamespaceHeader()
    {
        Advance(); // skip 'namespace'
        // Skip qualified name
        while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken && Current().Kind != SyntaxKind.SemicolonToken)
            Advance();
        // File-scoped namespace uses ;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            Advance();
            return;
        }
        // Block namespace uses {
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
            Advance();
    }

    // === Member Declarations ===

    private MemberDeclarationSyntax ParseMemberDeclaration()
    {
        // Skip attribute lists: [Flags], [Obsolete("msg")], etc.
        while (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBracketToken)
        {
            SkipAttributeList();
        }

        // Collect modifiers
        bool isPublic = false;
        bool isPrivate = false;
        bool isStatic = false;
        bool isAbstract = false;
        bool isVirtual = false;
        bool isOverride = false;

        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.PublicKeyword) { isPublic = true; Advance(); }
            else if (kind == SyntaxKind.PrivateKeyword) { isPrivate = true; Advance(); }
            else if (kind == SyntaxKind.ProtectedKeyword) { Advance(); }
            else if (kind == SyntaxKind.InternalKeyword) { Advance(); }
            else if (kind == SyntaxKind.StaticKeyword) { isStatic = true; Advance(); }
            else if (kind == SyntaxKind.AbstractKeyword) { isAbstract = true; Advance(); }
            else if (kind == SyntaxKind.VirtualKeyword) { isVirtual = true; Advance(); }
            else if (kind == SyntaxKind.OverrideKeyword) { isOverride = true; Advance(); }
            else if (kind == SyntaxKind.ReadOnlyKeyword) { Advance(); }
            else if (kind == SyntaxKind.SealedKeyword) { Advance(); }
            else if (kind == SyntaxKind.NewKeyword)
            {
                // 'new' as modifier (method hiding) — only if next token is NOT '(' or a type
                // Disambiguate from 'new Type()' expression
                if (_position + 1 < _tokens.Length)
                {
                    var next = _tokens[_position + 1].Kind;
                    if (next == SyntaxKind.OpenParenToken || next == SyntaxKind.IdentifierToken
                        || IsTypeStart(next))
                    {
                        // Could be 'new Type(...)' — don't treat as modifier
                        break;
                    }
                }
                Advance();
            }
            else if (kind == SyntaxKind.ConstKeyword) { Advance(); }
            else if (kind == SyntaxKind.VolatileKeyword) { Advance(); }
            else if (kind == SyntaxKind.ExternKeyword) { Advance(); }
            else if (kind == SyntaxKind.AsyncKeyword || (kind == SyntaxKind.IdentifierToken && Current().Text == "async")) { Advance(); }
            else if (kind == SyntaxKind.UnsafeKeyword) { Advance(); }
            else if (kind == SyntaxKind.PartialKeyword) { Advance(); }
            else break;
        }

        if (IsAtEnd()) return null;

        // Check for class declaration
        if (Current().Kind == SyntaxKind.ClassKeyword)
        {
            return ParseClassDeclaration();
        }

        // Check for enum declaration
        if (Current().Kind == SyntaxKind.EnumKeyword)
        {
            return ParseEnumDeclaration();
        }

        // Check for struct declaration
        if (Current().Kind == SyntaxKind.StructKeyword)
        {
            return ParseStructDeclaration();
        }

        // Must be a method, field, property, or constructor: type followed by name
        string typeName = ParseTypeName();
        if (typeName == null || IsAtEnd()) return null;

        // Constructor detection: if the parsed "type name" matches the current class name
        // and the next token is '(', this is a constructor (no return type, just ClassName(...))
        if (typeName == _currentClassName && Current().Kind == SyntaxKind.OpenParenToken)
        {
            return ParseConstructorDeclaration(typeName);
        }

        string name = Current().Text;
        Advance();

        // Method: has ( after name
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenParenToken)
        {
            return ParseMethodDeclaration(typeName, name, isStatic);
        }

        // Property detection: { get or { set pattern
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            int saved = _position;
            Advance(); // skip {
            if (!IsAtEnd() && (Current().Text == "get" || Current().Text == "set"))
            {
                _position = saved; // restore
                return ParsePropertyDeclaration(typeName, name, isStatic);
            }
            _position = saved; // restore — it's a method body or block
        }

        // Expression-bodied property: => expr;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsGreaterThanToken)
        {
            Advance(); // skip =>
            var exprBody = ParseExpression();
            Expect(SyntaxKind.SemicolonToken);
            return new PropertyDeclarationSyntax(typeName, name, true, false, exprBody, isStatic);
        }

        // Field: has = or ; after name
        ExpressionSyntax initializer = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
        {
            Advance();
            initializer = ParseExpression();
        }
        Expect(SyntaxKind.SemicolonToken);

        return new FieldDeclarationSyntax(typeName, name, initializer, isStatic);
    }

    private ClassDeclarationSyntax ParseClassDeclaration()
    {
        Advance(); // skip 'class'
        string name = Current().Text;
        Advance();

        // Skip generic type parameters: class Foo<T, U>
        if (!IsAtEnd() && Current().Kind == SyntaxKind.LessThanToken)
        {
            int depth = 0;
            while (!IsAtEnd())
            {
                if (Current().Kind == SyntaxKind.LessThanToken) depth++;
                else if (Current().Kind == SyntaxKind.GreaterThanToken)
                {
                    depth--;
                    if (depth == 0) { Advance(); break; }
                }
                Advance();
            }
        }

        // Parse base class/interface list — capture first base type name
        string baseTypeName = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance(); // skip ':'
            // First identifier after ':' is the base type
            if (!IsAtEnd() && Current().Kind == SyntaxKind.IdentifierToken)
            {
                baseTypeName = Current().Text;
            }
            // Skip remaining base list tokens until '{'
            while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken)
                Advance();
        }

        Expect(SyntaxKind.OpenBraceToken);

        string previousClassName = _currentClassName;
        _currentClassName = name;

        var members = new MemberDeclarationSyntax[256];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var member = ParseMemberDeclaration();
            if (member != null && count < 256)
            {
                members[count] = member;
                count++;
            }
        }

        _currentClassName = previousClassName;

        Expect(SyntaxKind.CloseBraceToken);

        var result = new MemberDeclarationSyntax[count];
        for (int i = 0; i < count; i++)
            result[i] = members[i];

        return new ClassDeclarationSyntax(name, baseTypeName, result);
    }

    private EnumDeclarationSyntax ParseEnumDeclaration()
    {
        Advance(); // skip 'enum'
        string name = Current().Text;
        Advance();

        // Skip base type if present
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance();
            ParseTypeName(); // skip base type
        }

        Expect(SyntaxKind.OpenBraceToken);

        var members = new EnumMemberSyntax[256];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            string memberName = Current().Text;
            Advance();

            ExpressionSyntax value = null;
            if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
            {
                Advance();
                value = ParseExpression();
            }

            if (count < 256)
            {
                members[count] = new EnumMemberSyntax(memberName, value);
                count++;
            }

            if (!IsAtEnd() && Current().Kind == SyntaxKind.CommaToken)
                Advance();
        }

        Expect(SyntaxKind.CloseBraceToken);

        var result = new EnumMemberSyntax[count];
        for (int i = 0; i < count; i++)
            result[i] = members[i];

        return new EnumDeclarationSyntax(name, result);
    }

    private StructDeclarationSyntax ParseStructDeclaration()
    {
        Advance(); // skip 'struct'
        string name = Current().Text;
        Advance();

        // Skip generic type parameters: struct Foo<T>
        if (!IsAtEnd() && Current().Kind == SyntaxKind.LessThanToken)
        {
            int depth = 0;
            while (!IsAtEnd())
            {
                if (Current().Kind == SyntaxKind.LessThanToken) depth++;
                else if (Current().Kind == SyntaxKind.GreaterThanToken)
                {
                    depth--;
                    if (depth == 0) { Advance(); break; }
                }
                Advance();
            }
        }

        // Parse base/interface list — capture first base type name
        string baseTypeName = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance(); // skip ':'
            if (!IsAtEnd() && Current().Kind == SyntaxKind.IdentifierToken)
            {
                baseTypeName = Current().Text;
            }
            while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken)
                Advance();
        }

        Expect(SyntaxKind.OpenBraceToken);

        string previousClassName = _currentClassName;
        _currentClassName = name;

        var members = new MemberDeclarationSyntax[256];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var member = ParseMemberDeclaration();
            if (member != null && count < 256)
            {
                members[count] = member;
                count++;
            }
        }

        _currentClassName = previousClassName;

        Expect(SyntaxKind.CloseBraceToken);

        var result = new MemberDeclarationSyntax[count];
        for (int i = 0; i < count; i++)
            result[i] = members[i];

        return new StructDeclarationSyntax(name, baseTypeName, result);
    }

    private PropertyDeclarationSyntax ParsePropertyDeclaration(string typeName, string name, bool isStatic)
    {
        Expect(SyntaxKind.OpenBraceToken);

        bool hasGetter = false;
        bool hasSetter = false;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            if (Current().Text == "get")
            {
                hasGetter = true;
                Advance();
                if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken) Advance();
                else if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken) SkipBlock();
            }
            else if (Current().Text == "set")
            {
                hasSetter = true;
                Advance();
                if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken) Advance();
                else if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken) SkipBlock();
            }
            else
            {
                Advance(); // skip unknown accessor modifiers
            }
        }

        Expect(SyntaxKind.CloseBraceToken);

        return new PropertyDeclarationSyntax(typeName, name, hasGetter, hasSetter, null, isStatic);
    }

    private ConstructorDeclarationSyntax ParseConstructorDeclaration(string name)
    {
        Advance(); // skip (

        var parameters = new ParameterSyntax[16];
        int paramCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (paramCount > 0)
                Expect(SyntaxKind.CommaToken);

            // Skip parameter modifiers: params, ref, out, in, this
            SkipParameterModifiers();

            string paramType = ParseTypeName();
            string paramName = Current().Text;
            Advance();

            // Skip default value: = expr
            if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
            {
                Advance(); // skip =
                ParseExpression(); // consume default value
            }

            if (paramCount < 16)
            {
                parameters[paramCount] = new ParameterSyntax(paramType, paramName);
                paramCount++;
            }
        }

        Expect(SyntaxKind.CloseParenToken);

        var paramResult = new ParameterSyntax[paramCount];
        for (int i = 0; i < paramCount; i++)
            paramResult[i] = parameters[i];

        // Parse constructor initializer (: base(...) or : this(...))
        string baseOrThisKeyword = null;
        var initializerArgs = new ExpressionSyntax[0];

        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance(); // skip :
            if (!IsAtEnd() && (Current().Kind == SyntaxKind.BaseKeyword || Current().Kind == SyntaxKind.ThisKeyword))
            {
                baseOrThisKeyword = Current().Text;
                Advance();

                Expect(SyntaxKind.OpenParenToken);

                var args = new ExpressionSyntax[16];
                int argCount = 0;

                while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
                {
                    if (argCount > 0)
                        Expect(SyntaxKind.CommaToken);
                    if (argCount < 16)
                    {
                        args[argCount] = ParseExpression();
                        argCount++;
                    }
                }

                Expect(SyntaxKind.CloseParenToken);

                initializerArgs = new ExpressionSyntax[argCount];
                for (int i = 0; i < argCount; i++)
                    initializerArgs[i] = args[i];
            }
            else
            {
                // Unknown initializer — skip to body
                while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken && Current().Kind != SyntaxKind.SemicolonToken)
                    Advance();
            }
        }

        BlockSyntax body = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            body = ParseBlock();
        }
        else if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            Advance();
        }

        return new ConstructorDeclarationSyntax(name, paramResult, body, baseOrThisKeyword, initializerArgs);
    }

    private void SkipBlock()
    {
        int depth = 0;
        if (Current().Kind == SyntaxKind.OpenBraceToken)
        {
            depth = 1;
            Advance();
        }
        while (!IsAtEnd() && depth > 0)
        {
            if (Current().Kind == SyntaxKind.OpenBraceToken) depth++;
            else if (Current().Kind == SyntaxKind.CloseBraceToken) depth--;
            Advance();
        }
    }

    private void SkipAttributeList()
    {
        if (Current().Kind != SyntaxKind.OpenBracketToken) return;
        int depth = 1;
        Advance(); // skip [
        while (!IsAtEnd() && depth > 0)
        {
            if (Current().Kind == SyntaxKind.OpenBracketToken) depth++;
            else if (Current().Kind == SyntaxKind.CloseBracketToken) depth--;
            Advance();
        }
    }

    private void SkipParameterModifiers()
    {
        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.ParamsKeyword
                || kind == SyntaxKind.RefKeyword
                || kind == SyntaxKind.OutKeyword
                || kind == SyntaxKind.InKeyword
                || kind == SyntaxKind.ThisKeyword)
            {
                Advance();
            }
            else break;
        }
    }

    private MethodDeclarationSyntax ParseMethodDeclaration(string returnType, string name, bool isStatic)
    {
        Advance(); // skip (

        var parameters = new ParameterSyntax[16];
        int paramCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (paramCount > 0)
                Expect(SyntaxKind.CommaToken);

            // Skip parameter modifiers: params, ref, out, in, this
            SkipParameterModifiers();

            string paramType = ParseTypeName();
            string paramName = Current().Text;
            Advance();

            // Skip default value: = expr
            if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
            {
                Advance(); // skip =
                ParseExpression(); // consume default value
            }

            if (paramCount < 16)
            {
                parameters[paramCount] = new ParameterSyntax(paramType, paramName);
                paramCount++;
            }
        }

        Expect(SyntaxKind.CloseParenToken);

        var paramResult = new ParameterSyntax[paramCount];
        for (int i = 0; i < paramCount; i++)
            paramResult[i] = parameters[i];

        // Skip constructor initializer (: base(...))
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken && Current().Kind != SyntaxKind.SemicolonToken)
                Advance();
        }

        BlockSyntax body = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            body = ParseBlock();
        }
        else if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsGreaterThanToken)
        {
            // Expression-bodied method: => expr;
            Advance(); // skip =>
            var expr = ParseExpression();
            Expect(SyntaxKind.SemicolonToken);
            var returnStmt = new ReturnStatementSyntax(expr);
            body = new BlockSyntax(new StatementSyntax[] { returnStmt });
        }
        else if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            // Abstract method without body
            Advance();
        }

        return new MethodDeclarationSyntax(returnType, name, paramResult, body, isStatic);
    }

    private string ParseTypeName()
    {
        if (IsAtEnd()) return null;

        string name = Current().Text;
        Advance();

        // Handle dotted qualifiers: SimpleTokenizer.TokenInfo, System.Collections.Generic
        while (!IsAtEnd() && Current().Kind == SyntaxKind.DotToken
            && _position + 1 < _tokens.Length && _tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
        {
            Advance(); // skip .
            name = name + "." + Current().Text;
            Advance(); // skip identifier
        }

        // Handle generic type parameters: List<int>, Dictionary<string, int>
        // Preserve generic args in the type name for MapType to resolve
        if (!IsAtEnd() && Current().Kind == SyntaxKind.LessThanToken)
        {
            name = name + "<";
            int depth = 0;
            while (!IsAtEnd())
            {
                if (Current().Kind == SyntaxKind.LessThanToken) { depth++; if (depth > 1) name = name + "<"; Advance(); }
                else if (Current().Kind == SyntaxKind.GreaterThanToken)
                {
                    depth--;
                    if (depth == 0) { name = name + ">"; Advance(); break; }
                    name = name + ">";
                    Advance();
                }
                else
                {
                    // Append the token text (for type args like int, string, commas)
                    if (Current().Kind != SyntaxKind.WhitespaceTrivia)
                        name = name + Current().Text;
                    Advance();
                }
            }
        }

        // Handle array types: int[] (only empty brackets, not int[size])
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBracketToken
            && _position + 1 < _tokens.Length && _tokens[_position + 1].Kind == SyntaxKind.CloseBracketToken)
        {
            Advance(); // skip [
            Advance(); // skip ]
            name = name + "[]";
        }

        // Handle nullable: int? or int?. (QuestionDotToken in type context means nullable + start of next expression)
        if (!IsAtEnd() && Current().Kind == SyntaxKind.QuestionToken)
        {
            Advance();
            name = name + "?";
        }

        return name;
    }

    // === Statements ===

    private BlockSyntax ParseBlock()
    {
        Expect(SyntaxKind.OpenBraceToken);

        var statements = new StatementSyntax[128];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var stmt = ParseStatement();
            if (stmt != null && count < 128)
            {
                statements[count] = stmt;
                count++;
            }
        }

        Expect(SyntaxKind.CloseBraceToken);

        var result = new StatementSyntax[count];
        for (int i = 0; i < count; i++)
            result[i] = statements[i];

        return new BlockSyntax(result);
    }

    private StatementSyntax ParseStatement()
    {
        if (IsAtEnd()) return null;

        SyntaxKind kind = Current().Kind;

        if (kind == SyntaxKind.ReturnKeyword)
            return ParseReturnStatement();

        if (kind == SyntaxKind.IfKeyword)
            return ParseIfStatement();

        if (kind == SyntaxKind.WhileKeyword)
            return ParseWhileStatement();

        if (kind == SyntaxKind.ForKeyword)
            return ParseForStatement();

        if (kind == SyntaxKind.ForEachKeyword)
            return ParseForEachStatement();

        if (kind == SyntaxKind.DoKeyword)
            return ParseDoStatement();

        if (kind == SyntaxKind.BreakKeyword)
        {
            Advance();
            Expect(SyntaxKind.SemicolonToken);
            return new BreakStatementSyntax();
        }

        if (kind == SyntaxKind.ContinueKeyword)
        {
            Advance();
            Expect(SyntaxKind.SemicolonToken);
            return new ContinueStatementSyntax();
        }

        if (kind == SyntaxKind.SwitchKeyword)
            return ParseSwitchStatement();

        if (kind == SyntaxKind.TryKeyword)
            return ParseTryStatement();

        if (kind == SyntaxKind.ThrowKeyword)
            return ParseThrowStatement();

        if (kind == SyntaxKind.LockKeyword)
            return ParseLockStatement();

        if (kind == SyntaxKind.UsingKeyword)
            return ParseUsingStatement();

        if (kind == SyntaxKind.OpenBraceToken)
            return ParseBlock();

        // Try local declaration: type name = expr;
        // Heuristic: if current looks like a type (identifier/keyword) followed by an identifier
        // Exclude contextual keywords that look like identifiers (await, yield, async)
        bool isContextualKeyword = kind == SyntaxKind.IdentifierToken
            && (Current().Text == "await" || Current().Text == "yield" || Current().Text == "async");
        if (IsTypeStart(kind) && !isContextualKeyword && _position + 1 < _tokens.Length)
        {
            SyntaxKind next = _tokens[_position + 1].Kind;
            if (next == SyntaxKind.IdentifierToken)
            {
                return ParseLocalDeclaration();
            }
            // Handle nullable type: int? name
            if (next == SyntaxKind.QuestionToken && _position + 2 < _tokens.Length
                && _tokens[_position + 2].Kind == SyntaxKind.IdentifierToken)
            {
                return ParseLocalDeclaration();
            }
            // Handle array type: int[] name
            if (next == SyntaxKind.OpenBracketToken && _position + 3 < _tokens.Length)
            {
                if (_tokens[_position + 2].Kind == SyntaxKind.CloseBracketToken
                    && _tokens[_position + 3].Kind == SyntaxKind.IdentifierToken)
                {
                    return ParseLocalDeclaration();
                }
            }
        }

        // var declaration
        if (kind == SyntaxKind.IdentifierToken && Current().Text == "var" && _position + 1 < _tokens.Length)
        {
            if (_tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
            {
                return ParseLocalDeclaration();
            }
            // var (a, b) = expr; — tuple deconstruction
            if (_tokens[_position + 1].Kind == SyntaxKind.OpenParenToken)
            {
                return ParseTupleDeconstruction();
            }
        }

        // Expression statement
        var expr = ParseExpression();
        Expect(SyntaxKind.SemicolonToken);
        return new ExpressionStatementSyntax(expr);
    }

    private bool IsTypeStart(SyntaxKind kind)
    {
        return kind == SyntaxKind.IntKeyword
            || kind == SyntaxKind.StringKeyword
            || kind == SyntaxKind.BoolKeyword
            || kind == SyntaxKind.VoidKeyword
            || kind == SyntaxKind.FloatKeyword
            || kind == SyntaxKind.DoubleKeyword
            || kind == SyntaxKind.LongKeyword
            || kind == SyntaxKind.CharKeyword
            || kind == SyntaxKind.ObjectKeyword
            || kind == SyntaxKind.IdentifierToken;
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
        Advance(); // skip 'return'

        if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            Advance();
            return new ReturnStatementSyntax(null);
        }

        var expr = ParseExpression();
        Expect(SyntaxKind.SemicolonToken);
        return new ReturnStatementSyntax(expr);
    }

    private IfStatementSyntax ParseIfStatement()
    {
        Advance(); // skip 'if'
        Expect(SyntaxKind.OpenParenToken);
        var condition = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);

        var thenBody = ParseStatementOrBlock();

        StatementSyntax elseBody = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ElseKeyword)
        {
            Advance();
            elseBody = ParseStatementOrBlock();
        }

        return new IfStatementSyntax(condition, thenBody, elseBody);
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
        Advance(); // skip 'while'
        Expect(SyntaxKind.OpenParenToken);
        var condition = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);

        var body = ParseStatementOrBlock();
        return new WhileStatementSyntax(condition, body);
    }

    private ForStatementSyntax ParseForStatement()
    {
        Advance(); // skip 'for'
        Expect(SyntaxKind.OpenParenToken);

        // Parse initializer (declaration or expression statement, or empty)
        StatementSyntax declaration = null;
        if (!IsAtEnd() && Current().Kind != SyntaxKind.SemicolonToken)
        {
            // Try to detect local declaration: type name pattern
            SyntaxKind initKind = Current().Kind;
            if (IsTypeStart(initKind) && _position + 1 < _tokens.Length)
            {
                SyntaxKind next = _tokens[_position + 1].Kind;
                if (next == SyntaxKind.IdentifierToken)
                {
                    declaration = ParseLocalDeclarationNoSemicolon();
                }
                else
                {
                    // Expression initializer
                    var expr = ParseExpression();
                    declaration = new ExpressionStatementSyntax(expr);
                }
            }
            else if (initKind == SyntaxKind.IdentifierToken && Current().Text == "var" && _position + 1 < _tokens.Length
                && _tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
            {
                declaration = ParseLocalDeclarationNoSemicolon();
            }
            else
            {
                var expr = ParseExpression();
                declaration = new ExpressionStatementSyntax(expr);
            }
        }
        Expect(SyntaxKind.SemicolonToken);

        // Parse condition (or empty)
        ExpressionSyntax condition = null;
        if (!IsAtEnd() && Current().Kind != SyntaxKind.SemicolonToken)
        {
            condition = ParseExpression();
        }
        Expect(SyntaxKind.SemicolonToken);

        // Parse incrementors (comma-separated expressions)
        var incrementors = new ExpressionSyntax[16];
        int incCount = 0;
        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (incCount > 0)
                Expect(SyntaxKind.CommaToken);
            if (incCount < 16)
            {
                incrementors[incCount] = ParseExpression();
                incCount++;
            }
        }
        Expect(SyntaxKind.CloseParenToken);

        var incResult = new ExpressionSyntax[incCount];
        for (int i = 0; i < incCount; i++)
            incResult[i] = incrementors[i];

        var body = ParseStatementOrBlock();
        return new ForStatementSyntax(declaration, condition, incResult, body);
    }

    private LocalDeclarationStatementSyntax ParseLocalDeclarationNoSemicolon()
    {
        string typeName = ParseTypeName();
        string varName = Current().Text;
        Advance();

        ExpressionSyntax initializer = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
        {
            Advance();
            initializer = ParseExpression();
        }

        return new LocalDeclarationStatementSyntax(typeName, varName, initializer);
    }

    private ForEachStatementSyntax ParseForEachStatement()
    {
        Advance(); // skip 'foreach'
        Expect(SyntaxKind.OpenParenToken);

        string typeName = ParseTypeName();
        string identifier = Current().Text;
        Advance();

        // Expect 'in' keyword
        Expect(SyntaxKind.InKeyword);

        var expression = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);

        var body = ParseStatementOrBlock();
        return new ForEachStatementSyntax(typeName, identifier, expression, body);
    }

    private DoStatementSyntax ParseDoStatement()
    {
        Advance(); // skip 'do'
        var body = ParseStatementOrBlock();

        Expect(SyntaxKind.WhileKeyword);
        Expect(SyntaxKind.OpenParenToken);
        var condition = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);
        Expect(SyntaxKind.SemicolonToken);

        return new DoStatementSyntax(body, condition);
    }

    private SwitchStatementSyntax ParseSwitchStatement()
    {
        Advance(); // skip 'switch'
        Expect(SyntaxKind.OpenParenToken);
        var expression = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);

        Expect(SyntaxKind.OpenBraceToken);

        var sections = new SwitchSectionSyntax[64];
        int sectionCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            // Parse labels for this section
            var labels = new ExpressionSyntax[16];
            int labelCount = 0;

            while (!IsAtEnd() && (Current().Kind == SyntaxKind.CaseKeyword || Current().Kind == SyntaxKind.DefaultKeyword))
            {
                if (Current().Kind == SyntaxKind.CaseKeyword)
                {
                    Advance(); // skip 'case'
                    if (labelCount < 16)
                    {
                        labels[labelCount] = ParseExpression();
                        labelCount++;
                    }
                    Expect(SyntaxKind.ColonToken);
                }
                else // DefaultKeyword
                {
                    Advance(); // skip 'default'
                    if (labelCount < 16)
                    {
                        labels[labelCount] = null; // null = default label
                        labelCount++;
                    }
                    Expect(SyntaxKind.ColonToken);
                }
            }

            // Parse statements until next case/default/closing brace
            var statements = new StatementSyntax[128];
            int stmtCount = 0;

            while (!IsAtEnd()
                && Current().Kind != SyntaxKind.CaseKeyword
                && Current().Kind != SyntaxKind.DefaultKeyword
                && Current().Kind != SyntaxKind.CloseBraceToken)
            {
                var stmt = ParseStatement();
                if (stmt != null && stmtCount < 128)
                {
                    statements[stmtCount] = stmt;
                    stmtCount++;
                }
            }

            var labelResult = new ExpressionSyntax[labelCount];
            for (int i = 0; i < labelCount; i++)
                labelResult[i] = labels[i];

            var stmtResult = new StatementSyntax[stmtCount];
            for (int i = 0; i < stmtCount; i++)
                stmtResult[i] = statements[i];

            if (sectionCount < 64)
            {
                sections[sectionCount] = new SwitchSectionSyntax(labelResult, stmtResult);
                sectionCount++;
            }
        }

        Expect(SyntaxKind.CloseBraceToken);

        var sectionResult = new SwitchSectionSyntax[sectionCount];
        for (int i = 0; i < sectionCount; i++)
            sectionResult[i] = sections[i];

        return new SwitchStatementSyntax(expression, sectionResult);
    }

    private TryStatementSyntax ParseTryStatement()
    {
        Advance(); // skip 'try'
        var block = ParseBlock();

        var catches = new CatchClauseSyntax[8];
        int catchCount = 0;

        while (!IsAtEnd() && Current().Kind == SyntaxKind.CatchKeyword)
        {
            Advance(); // skip 'catch'

            string exceptionTypeName = null;
            string identifier = null;

            if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenParenToken)
            {
                Advance(); // skip (
                exceptionTypeName = ParseTypeName();

                // Optional identifier
                if (!IsAtEnd() && Current().Kind == SyntaxKind.IdentifierToken)
                {
                    identifier = Current().Text;
                    Advance();
                }

                Expect(SyntaxKind.CloseParenToken);
            }

            var catchBlock = ParseBlock();

            if (catchCount < 8)
            {
                catches[catchCount] = new CatchClauseSyntax(exceptionTypeName, identifier, catchBlock);
                catchCount++;
            }
        }

        BlockSyntax finallyBlock = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.FinallyKeyword)
        {
            Advance(); // skip 'finally'
            finallyBlock = ParseBlock();
        }

        var catchResult = new CatchClauseSyntax[catchCount];
        for (int i = 0; i < catchCount; i++)
            catchResult[i] = catches[i];

        return new TryStatementSyntax(block, catchResult, finallyBlock);
    }

    private ThrowStatementSyntax ParseThrowStatement()
    {
        Advance(); // skip 'throw'

        // throw; (re-throw) or throw expr;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            Advance();
            return new ThrowStatementSyntax(null);
        }

        var expr = ParseExpression();
        Expect(SyntaxKind.SemicolonToken);
        return new ThrowStatementSyntax(expr);
    }

    private LockStatementSyntax ParseLockStatement()
    {
        Advance(); // skip 'lock'
        Expect(SyntaxKind.OpenParenToken);
        var expr = ParseExpression();
        Expect(SyntaxKind.CloseParenToken);
        var block = ParseBlock();
        return new LockStatementSyntax(expr, block);
    }

    private StatementSyntax ParseUsingStatement()
    {
        Advance(); // skip 'using'

        // C# 8+ using declaration: using var x = expr; (no parens, no block)
        if (!IsAtEnd() && Current().Kind != SyntaxKind.OpenParenToken)
        {
            // using var x = expr; or using Type x = expr;
            string typeName = ParseTypeName();
            string varName = Current().Text;
            Advance(); // skip identifier
            ExpressionSyntax init = null;
            if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
            {
                Advance(); // skip '='
                init = ParseExpression();
            }
            Expect(SyntaxKind.SemicolonToken);
            // Emit as a plain local declaration (no Dispose in Luau)
            return new LocalDeclarationStatementSyntax(typeName, varName, init);
        }

        Expect(SyntaxKind.OpenParenToken);

        // using (Type name = expr) { } or using (expr) { }
        LocalDeclarationStatementSyntax decl = null;
        ExpressionSyntax usingExpr = null;

        bool isDecl = false;

        // Heuristic: if current is a type-start and next is identifier, it's a declaration
        if (IsTypeStart(Current().Kind) && _position + 1 < _tokens.Length
            && _tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
        {
            isDecl = true;
        }
        // Also check for var
        if (!IsAtEnd() && Current().Kind == SyntaxKind.IdentifierToken && Current().Text == "var"
            && _position + 1 < _tokens.Length && _tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
        {
            isDecl = true;
        }

        if (isDecl)
        {
            string typeName = ParseTypeName();
            string varName = Current().Text;
            Advance(); // skip identifier
            ExpressionSyntax init = null;
            if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
            {
                Advance(); // skip '='
                init = ParseExpression();
            }
            decl = new LocalDeclarationStatementSyntax(typeName, varName, init);
        }
        else
        {
            usingExpr = ParseExpression();
        }

        Expect(SyntaxKind.CloseParenToken);
        var block = ParseBlock();
        return new UsingStatementSyntax(decl, usingExpr, block);
    }

    private StatementSyntax ParseStatementOrBlock()
    {
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
            return ParseBlock();
        return ParseStatement();
    }

    private LocalDeclarationStatementSyntax ParseLocalDeclaration()
    {
        string typeName = ParseTypeName();
        string varName = Current().Text;
        Advance();

        ExpressionSyntax initializer = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
        {
            Advance();
            initializer = ParseExpression();
        }

        Expect(SyntaxKind.SemicolonToken);
        return new LocalDeclarationStatementSyntax(typeName, varName, initializer);
    }

    private StatementSyntax ParseTupleDeconstruction()
    {
        // var (a, b, c) = expr;
        Advance(); // skip 'var'
        Expect(SyntaxKind.OpenParenToken);

        string[] names = new string[8];
        int nameCount = 0;
        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (nameCount > 0) Expect(SyntaxKind.CommaToken);
            if (nameCount < 8)
            {
                names[nameCount] = Current().Text;
                nameCount = nameCount + 1;
            }
            Advance(); // consume identifier
        }
        Expect(SyntaxKind.CloseParenToken);
        Expect(SyntaxKind.EqualsToken);
        ExpressionSyntax initializer = ParseExpression();
        Expect(SyntaxKind.SemicolonToken);

        // Trim names array to actual count
        string[] trimmed = new string[nameCount];
        for (int i = 0; i < nameCount; i++)
        {
            trimmed[i] = names[i];
        }
        return new TupleDeconstructionStatementSyntax(trimmed, initializer);
    }

    // === Expressions (Pratt-style precedence climbing) ===

    public ExpressionSyntax ParseExpression()
    {
        return ParseAssignment();
    }

    private ExpressionSyntax ParseAssignment()
    {
        var left = ParseConditionalOr();

        if (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;

            if (kind == SyntaxKind.EqualsToken
                || kind == SyntaxKind.PlusEqualsToken
                || kind == SyntaxKind.MinusEqualsToken
                || kind == SyntaxKind.AsteriskEqualsToken
                || kind == SyntaxKind.SlashEqualsToken
                || kind == SyntaxKind.PercentEqualsToken
                || kind == SyntaxKind.QuestionQuestionEqualsToken)
            {
                var op = MakeSyntaxToken(Advance());
                var right = ParseAssignment();

                int exprKind = kind == SyntaxKind.EqualsToken ? (int)SyntaxKind.SimpleAssignmentExpression
                    : kind == SyntaxKind.PlusEqualsToken ? (int)SyntaxKind.AddAssignmentExpression
                    : kind == SyntaxKind.MinusEqualsToken ? (int)SyntaxKind.SubtractAssignmentExpression
                    : kind == SyntaxKind.AsteriskEqualsToken ? (int)SyntaxKind.MultiplyAssignmentExpression
                    : kind == SyntaxKind.SlashEqualsToken ? (int)SyntaxKind.DivideAssignmentExpression
                    : kind == SyntaxKind.PercentEqualsToken ? (int)SyntaxKind.ModuloAssignmentExpression
                    : (int)SyntaxKind.CoalesceAssignmentExpression;

                return new AssignmentExpressionSyntax(exprKind, left, op, right);
            }

            // Ternary conditional: expr ? trueExpr : falseExpr
            if (kind == SyntaxKind.QuestionToken)
            {
                Advance(); // skip ?
                var whenTrue = ParseExpression();
                Expect(SyntaxKind.ColonToken);
                var whenFalse = ParseExpression();
                return new ConditionalExpressionSyntax(left, whenTrue, whenFalse);
            }
        }

        return left;
    }

    private ExpressionSyntax ParseConditionalOr()
    {
        var left = ParseConditionalAnd();

        while (!IsAtEnd() && Current().Kind == SyntaxKind.BarBarToken)
        {
            var op = MakeSyntaxToken(Advance());
            var right = ParseConditionalAnd();
            left = new BinaryExpressionSyntax(8675, left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParseConditionalAnd()
    {
        var left = ParseNullCoalescing();

        while (!IsAtEnd() && Current().Kind == SyntaxKind.AmpersandAmpersandToken)
        {
            var op = MakeSyntaxToken(Advance());
            var right = ParseNullCoalescing();
            left = new BinaryExpressionSyntax(8676, left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParseNullCoalescing()
    {
        var left = ParseEquality();

        while (!IsAtEnd() && Current().Kind == SyntaxKind.QuestionQuestionToken)
        {
            var op = MakeSyntaxToken(Advance());
            var right = ParseEquality();
            left = new BinaryExpressionSyntax(8688, left, op, right); // CoalesceExpression
        }

        return left;
    }

    private ExpressionSyntax ParseEquality()
    {
        var left = ParseRelational();

        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.EqualsEqualsToken || kind == SyntaxKind.ExclamationEqualsToken)
            {
                var op = MakeSyntaxToken(Advance());
                var right = ParseRelational();
                int exprKind = kind == SyntaxKind.EqualsEqualsToken ? 8682 : 8683;
                left = new BinaryExpressionSyntax(exprKind, left, op, right);
            }
            else break;
        }

        return left;
    }

    private ExpressionSyntax ParseRelational()
    {
        var left = ParseAdditive();

        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.LessThanToken || kind == SyntaxKind.GreaterThanToken
                || kind == SyntaxKind.LessThanEqualsToken || kind == SyntaxKind.GreaterThanEqualsToken)
            {
                var op = MakeSyntaxToken(Advance());
                var right = ParseAdditive();
                int exprKind = kind == SyntaxKind.LessThanToken ? 8678
                    : kind == SyntaxKind.GreaterThanToken ? 8680
                    : kind == SyntaxKind.LessThanEqualsToken ? 8679
                    : 8681;
                left = new BinaryExpressionSyntax(exprKind, left, op, right);
            }
            else if (kind == SyntaxKind.IsKeyword)
            {
                // is type-pattern: expr is TypeName optionalIdentifier
                var op = MakeSyntaxToken(Advance()); // skip 'is'
                string typeName = ParseTypeName();

                // Check for pattern variable: is TypeName varName
                if (!IsAtEnd() && Current().Kind == SyntaxKind.IdentifierToken)
                {
                    string designation = Current().Text;
                    Advance(); // consume pattern variable
                    left = new IsPatternExpressionSyntax(left, typeName, designation);
                }
                else
                {
                    var typeIdent = new SyntaxToken((int)SyntaxKind.IdentifierToken, typeName, 0, typeName.Length);
                    var right = new IdentifierNameSyntax(typeIdent);
                    left = new BinaryExpressionSyntax(8657, left, op, right); // IsExpression
                }
            }
            else if (kind == SyntaxKind.AsKeyword)
            {
                // as cast: expr as TypeName
                var op = MakeSyntaxToken(Advance()); // skip 'as'
                string typeName = ParseTypeName();
                var typeIdent = new SyntaxToken((int)SyntaxKind.IdentifierToken, typeName, 0, typeName.Length);
                var right = new IdentifierNameSyntax(typeIdent);
                left = new BinaryExpressionSyntax(8658, left, op, right); // AsExpression
            }
            else break;
        }

        return left;
    }

    private ExpressionSyntax ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.PlusToken || kind == SyntaxKind.MinusToken)
            {
                var op = MakeSyntaxToken(Advance());
                var right = ParseMultiplicative();
                int exprKind = kind == SyntaxKind.PlusToken ? 8668 : 8669;
                left = new BinaryExpressionSyntax(exprKind, left, op, right);
            }
            else break;
        }

        return left;
    }

    private ExpressionSyntax ParseMultiplicative()
    {
        var left = ParseUnary();

        while (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.AsteriskToken || kind == SyntaxKind.SlashToken || kind == SyntaxKind.PercentToken)
            {
                var op = MakeSyntaxToken(Advance());
                var right = ParseUnary();
                int exprKind = kind == SyntaxKind.AsteriskToken ? 8670
                    : kind == SyntaxKind.SlashToken ? 8671
                    : 8672;
                left = new BinaryExpressionSyntax(exprKind, left, op, right);
            }
            else break;
        }

        return left;
    }

    private ExpressionSyntax ParseUnary()
    {
        if (!IsAtEnd())
        {
            SyntaxKind kind = Current().Kind;
            if (kind == SyntaxKind.MinusToken || kind == SyntaxKind.ExclamationToken)
            {
                var op = MakeSyntaxToken(Advance());
                var operand = ParseUnary();
                int exprKind = kind == SyntaxKind.MinusToken ? 8730 : 8731;
                return new PrefixUnaryExpressionSyntax(exprKind, op, operand);
            }

            // await expr
            if (kind == SyntaxKind.AwaitKeyword || (kind == SyntaxKind.IdentifierToken && Current().Text == "await"))
            {
                var op = MakeSyntaxToken(Advance());
                var operand = ParseUnary();
                return new PrefixUnaryExpressionSyntax(8740, op, operand); // AwaitExpression
            }

            // Prefix ++/--
            if (kind == SyntaxKind.PlusPlusToken || kind == SyntaxKind.MinusMinusToken)
            {
                var op = MakeSyntaxToken(Advance());
                var operand = ParseUnary();
                int exprKind = kind == SyntaxKind.PlusPlusToken
                    ? (int)SyntaxKind.PreIncrementExpression
                    : (int)SyntaxKind.PreDecrementExpression;
                return new PrefixUnaryExpressionSyntax(exprKind, op, operand);
            }
        }

        return ParsePrimary();
    }

    private ExpressionSyntax ParsePrimary()
    {
        var expr = ParseAtom();

        // Handle postfix: member access, invocation, element access, postfix ++/--
        while (!IsAtEnd())
        {
            if (Current().Kind == SyntaxKind.DotToken)
            {
                Advance(); // skip .
                var name = MakeSyntaxToken(Advance());
                expr = new MemberAccessExpressionSyntax(expr, name);
            }
            else if (Current().Kind == SyntaxKind.QuestionDotToken)
            {
                Advance(); // skip ?.
                var name = MakeSyntaxToken(Advance());
                var memberAccess = new MemberAccessExpressionSyntax(expr, name);
                // If followed by ( → wrap the invocation inside the conditional
                if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenParenToken)
                {
                    Advance(); // skip (
                    var args = new ExpressionSyntax[16];
                    int argCount = 0;
                    while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
                    {
                        if (argCount > 0)
                            Expect(SyntaxKind.CommaToken);
                        if (argCount < 16)
                        {
                            args[argCount] = ParseExpression();
                            argCount++;
                        }
                    }
                    Expect(SyntaxKind.CloseParenToken);
                    var argResult = new ExpressionSyntax[argCount];
                    for (int i = 0; i < argCount; i++)
                        argResult[i] = args[i];
                    var invocation = new InvocationExpressionSyntax(memberAccess, argResult);
                    expr = new ConditionalAccessExpressionSyntax(expr, invocation);
                }
                else
                {
                    expr = new ConditionalAccessExpressionSyntax(expr, memberAccess);
                }
            }
            else if (Current().Kind == SyntaxKind.OpenParenToken)
            {
                Advance(); // skip (
                var args = new ExpressionSyntax[16];
                int argCount = 0;

                while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
                {
                    if (argCount > 0)
                        Expect(SyntaxKind.CommaToken);
                    if (argCount < 16)
                    {
                        args[argCount] = ParseExpression();
                        argCount++;
                    }
                }
                Expect(SyntaxKind.CloseParenToken);

                var argResult = new ExpressionSyntax[argCount];
                for (int i = 0; i < argCount; i++)
                    argResult[i] = args[i];

                expr = new InvocationExpressionSyntax(expr, argResult);
            }
            else if (Current().Kind == SyntaxKind.OpenBracketToken)
            {
                Advance(); // skip [
                var index = ParseExpression();
                Expect(SyntaxKind.CloseBracketToken);
                expr = new ElementAccessExpressionSyntax(expr, index);
            }
            else if (Current().Kind == SyntaxKind.PlusPlusToken || Current().Kind == SyntaxKind.MinusMinusToken)
            {
                var opKind = Current().Kind;
                Advance();
                int exprKind = opKind == SyntaxKind.PlusPlusToken
                    ? (int)SyntaxKind.PostIncrementExpression
                    : (int)SyntaxKind.PostDecrementExpression;
                expr = new PostfixUnaryExpressionSyntax(exprKind, expr, opKind);
            }
            else if (Current().Kind == SyntaxKind.SwitchKeyword)
            {
                // Switch expression: expr switch { pattern => value, ... }
                expr = ParseSwitchExpression(expr);
            }
            else break;
        }

        return expr;
    }

    private ExpressionSyntax ParseAtom()
    {
        if (IsAtEnd())
            return new LiteralExpressionSyntax(8753, new SyntaxToken(0, "", 0, 0));

        SyntaxKind kind = Current().Kind;

        // Numeric literal
        if (kind == SyntaxKind.NumericLiteralToken)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8750, token);
        }

        // String literal
        if (kind == SyntaxKind.StringLiteralToken)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8751, token);
        }

        // Interpolated string: $"..." → treated as InterpolatedStringExpression (8655)
        if (kind == SyntaxKind.InterpolatedStringToken)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8655, token);
        }

        // Character literal
        if (kind == SyntaxKind.CharacterLiteralToken)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8752, token);
        }

        // true/false
        if (kind == SyntaxKind.TrueKeyword)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8748, token);
        }
        if (kind == SyntaxKind.FalseKeyword)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8749, token);
        }

        // null
        if (kind == SyntaxKind.NullKeyword)
        {
            var token = MakeSyntaxToken(Advance());
            return new LiteralExpressionSyntax(8754, token);
        }

        // new keyword → object creation
        if (kind == SyntaxKind.NewKeyword)
        {
            return ParseObjectCreation();
        }

        // Parenthesized lambda or parenthesized expression or cast
        if (kind == SyntaxKind.OpenParenToken)
        {
            // Check for parenthesized lambda: (...) =>
            if (IsParenthesizedLambda())
            {
                return ParseParenthesizedLambda();
            }

            // Look ahead to detect cast: (Type)expr
            int saved = _position;
            Advance(); // skip (
            if (!IsAtEnd() && IsTypeStart(Current().Kind))
            {
                string possibleType = Current().Text;
                int savedInner = _position;
                Advance();
                // Check for closing paren right after type name
                if (!IsAtEnd() && Current().Kind == SyntaxKind.CloseParenToken)
                {
                    // Check if next token starts an expression (not an operator)
                    int savedAfterParen = _position;
                    Advance(); // skip )
                    if (!IsAtEnd() && IsExpressionStart(Current().Kind))
                    {
                        // It's a cast
                        var operand = ParseUnary();
                        return new CastExpressionSyntax(possibleType, operand);
                    }
                    _position = savedAfterParen; // not a cast, restore
                }
                _position = savedInner; // not a cast
            }
            _position = saved; // not a cast, restore to before (

            // Regular parenthesized expression
            Advance(); // skip (
            var inner = ParseExpression();
            Expect(SyntaxKind.CloseParenToken);
            return new ParenthesizedExpressionSyntax(inner);
        }

        // Simple lambda: identifier => body
        if (kind == SyntaxKind.IdentifierToken && _position + 1 < _tokens.Length
            && _tokens[_position + 1].Kind == SyntaxKind.EqualsGreaterThanToken)
        {
            return ParseSimpleLambda();
        }

        // this / base keywords — treat as identifiers in expression position
        if (kind == SyntaxKind.ThisKeyword || kind == SyntaxKind.BaseKeyword)
        {
            var token = MakeSyntaxToken(Advance());
            return new IdentifierNameSyntax(token);
        }

        // Identifier
        if (kind == SyntaxKind.IdentifierToken)
        {
            var token = MakeSyntaxToken(Advance());
            return new IdentifierNameSyntax(token);
        }

        // Keyword used as type (int, string, etc.) — treat as identifier
        if (IsTypeStart(kind))
        {
            var token = MakeSyntaxToken(Advance());
            return new IdentifierNameSyntax(token);
        }

        // Unknown — advance past it to avoid infinite loop
        var unknown = MakeSyntaxToken(Advance());
        return new LiteralExpressionSyntax(8753, unknown);
    }

    private SwitchExpressionSyntax ParseSwitchExpression(ExpressionSyntax governing)
    {
        Advance(); // skip 'switch'
        Expect(SyntaxKind.OpenBraceToken);

        var arms = new SwitchExpressionArmSyntax[64];
        int armCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            if (armCount > 0 && Current().Kind == SyntaxKind.CommaToken)
                Advance(); // skip comma between arms
            if (Current().Kind == SyntaxKind.CloseBraceToken)
                break;

            // Parse pattern (or _ for discard)
            ExpressionSyntax pattern = null;
            if (Current().Text == "_")
            {
                Advance(); // skip _
                pattern = null; // discard pattern
            }
            else
            {
                pattern = ParseExpression();
            }

            Expect(SyntaxKind.EqualsGreaterThanToken);
            var value = ParseExpression();

            if (armCount < 64)
            {
                arms[armCount] = new SwitchExpressionArmSyntax(pattern, value);
                armCount++;
            }
        }

        Expect(SyntaxKind.CloseBraceToken);

        var result = new SwitchExpressionArmSyntax[armCount];
        for (int i = 0; i < armCount; i++)
            result[i] = arms[i];

        return new SwitchExpressionSyntax(governing, result);
    }

    private ExpressionSyntax ParseObjectCreation()
    {
        Advance(); // skip 'new'
        string typeName = ParseTypeName();

        // Detect array creation: new Type[size]
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBracketToken)
        {
            Advance(); // skip [
            ExpressionSyntax sizeExpr = null;
            if (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBracketToken)
            {
                sizeExpr = ParseExpression();
            }
            Expect(SyntaxKind.CloseBracketToken);
            return new ArrayCreationExpressionSyntax(typeName, sizeExpr);
        }

        var args = new ExpressionSyntax[16];
        int argCount = 0;

        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenParenToken)
        {
            Advance(); // skip (
            while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
            {
                if (argCount > 0)
                    Expect(SyntaxKind.CommaToken);
                if (argCount < 16)
                {
                    args[argCount] = ParseExpression();
                    argCount++;
                }
            }
            Expect(SyntaxKind.CloseParenToken);
        }

        // Parse object initializer if present: { Field = value, ... }
        // For array types (T[]), parse as collection initializer: { expr, ... }
        AssignmentExpressionSyntax[] initializers = null;
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            bool isArrayType = typeName.Length > 2 && typeName[typeName.Length - 1] == ']' && typeName[typeName.Length - 2] == '[';

            if (isArrayType)
            {
                // Collection initializer: new T[] { a, b, c } → store elements as args
                Advance(); // skip {
                while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
                {
                    if (argCount > 0 && Current().Kind == SyntaxKind.CommaToken)
                        Advance();
                    if (Current().Kind == SyntaxKind.CloseBraceToken)
                        break;
                    if (argCount < 16)
                    {
                        args[argCount] = ParseExpression();
                        argCount++;
                    }
                }
                Expect(SyntaxKind.CloseBraceToken);
            }
            else
            {
                Advance(); // skip {
                var inits = new AssignmentExpressionSyntax[32];
                int initCount = 0;

                while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
                {
                    if (initCount > 0 && Current().Kind == SyntaxKind.CommaToken)
                        Advance(); // skip comma
                    if (Current().Kind == SyntaxKind.CloseBraceToken)
                        break;

                    var expr = ParseExpression();
                    if (expr is AssignmentExpressionSyntax assign && initCount < 32)
                    {
                        inits[initCount] = assign;
                        initCount++;
                    }
                }

                Expect(SyntaxKind.CloseBraceToken);

                initializers = new AssignmentExpressionSyntax[initCount];
                for (int i = 0; i < initCount; i++)
                    initializers[i] = inits[i];
            }
        }

        var result = new ExpressionSyntax[argCount];
        for (int i = 0; i < argCount; i++)
            result[i] = args[i];

        return new ObjectCreationExpressionSyntax(typeName, result, initializers);
    }

    private bool IsExpressionStart(SyntaxKind kind)
    {
        return kind == SyntaxKind.IdentifierToken
            || kind == SyntaxKind.NumericLiteralToken
            || kind == SyntaxKind.StringLiteralToken
            || kind == SyntaxKind.CharacterLiteralToken
            || kind == SyntaxKind.TrueKeyword
            || kind == SyntaxKind.FalseKeyword
            || kind == SyntaxKind.NullKeyword
            || kind == SyntaxKind.OpenParenToken
            || kind == SyntaxKind.NewKeyword
            || kind == SyntaxKind.MinusToken
            || kind == SyntaxKind.ExclamationToken;
    }

    // === Lambda Parsing ===

    /// <summary>
    /// Lookahead to detect parenthesized lambda: scan for matching ) then =>.
    /// Handles: () =>, (x) =>, (x, y) =>, (int x) =>, (int x, string y) => etc.
    /// </summary>
    private bool IsParenthesizedLambda()
    {
        if (Current().Kind != SyntaxKind.OpenParenToken)
            return false;

        int saved = _position;
        int depth = 0;

        // Scan for matching close paren
        for (int i = _position; i < _tokens.Length; i++)
        {
            SyntaxKind k = _tokens[i].Kind;
            if (k == SyntaxKind.OpenParenToken) depth++;
            else if (k == SyntaxKind.CloseParenToken)
            {
                depth--;
                if (depth == 0)
                {
                    // Check if next token after ) is =>
                    if (i + 1 < _tokens.Length && _tokens[i + 1].Kind == SyntaxKind.EqualsGreaterThanToken)
                        return true;
                    return false;
                }
            }
            // If we hit a semicolon or brace before closing paren, it's not a lambda
            else if (k == SyntaxKind.SemicolonToken || k == SyntaxKind.OpenBraceToken)
                return false;
        }

        return false;
    }

    /// <summary>
    /// Parse simple lambda: identifier => body
    /// </summary>
    private LambdaExpressionSyntax ParseSimpleLambda()
    {
        string paramName = Current().Text;
        Advance(); // skip identifier
        Advance(); // skip =>

        var paramNames = new string[] { paramName };

        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            var block = ParseBlock();
            return new LambdaExpressionSyntax(paramNames, null, block);
        }
        else
        {
            var body = ParseExpression();
            return new LambdaExpressionSyntax(paramNames, body, null);
        }
    }

    /// <summary>
    /// Parse parenthesized lambda: (params) => body
    /// Handles typed and untyped parameter lists.
    /// </summary>
    private LambdaExpressionSyntax ParseParenthesizedLambda()
    {
        Advance(); // skip (

        var paramNames = new string[16];
        int paramCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (paramCount > 0)
                Expect(SyntaxKind.CommaToken);

            // Check if this is a typed parameter (type name) or untyped (just name)
            // Heuristic: if the token after the current identifier is also an identifier
            // or close paren/comma, the current one could be a type. Check if next is
            // an identifier (typed param) — e.g., "int x" vs just "x"
            if (IsTypeStart(Current().Kind) && _position + 1 < _tokens.Length
                && _tokens[_position + 1].Kind == SyntaxKind.IdentifierToken)
            {
                // Typed parameter: skip the type, take the name
                ParseTypeName();
                if (paramCount < 16)
                {
                    paramNames[paramCount] = Current().Text;
                    paramCount++;
                }
                Advance();
            }
            else
            {
                // Untyped parameter: just a name
                if (paramCount < 16)
                {
                    paramNames[paramCount] = Current().Text;
                    paramCount++;
                }
                Advance();
            }
        }

        Expect(SyntaxKind.CloseParenToken);
        Advance(); // skip =>

        var paramResult = new string[paramCount];
        for (int i = 0; i < paramCount; i++)
            paramResult[i] = paramNames[i];

        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
        {
            var block = ParseBlock();
            return new LambdaExpressionSyntax(paramResult, null, block);
        }
        else
        {
            var body = ParseExpression();
            return new LambdaExpressionSyntax(paramResult, body, null);
        }
    }
}
