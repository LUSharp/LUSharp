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
        var members = new MemberDeclarationSyntax[64];
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
            if (member != null && count < 64)
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

        // Skip base class/interface list
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance();
            while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken)
                Advance();
        }

        Expect(SyntaxKind.OpenBraceToken);

        string previousClassName = _currentClassName;
        _currentClassName = name;

        var members = new MemberDeclarationSyntax[64];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var member = ParseMemberDeclaration();
            if (member != null && count < 64)
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

        return new ClassDeclarationSyntax(name, result);
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

        // Skip base/interface list
        if (!IsAtEnd() && Current().Kind == SyntaxKind.ColonToken)
        {
            Advance();
            while (!IsAtEnd() && Current().Kind != SyntaxKind.OpenBraceToken)
                Advance();
        }

        Expect(SyntaxKind.OpenBraceToken);

        string previousClassName = _currentClassName;
        _currentClassName = name;

        var members = new MemberDeclarationSyntax[64];
        int count = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseBraceToken)
        {
            var member = ParseMemberDeclaration();
            if (member != null && count < 64)
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

        return new StructDeclarationSyntax(name, result);
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

            string paramType = ParseTypeName();
            string paramName = Current().Text;
            Advance();

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

    private MethodDeclarationSyntax ParseMethodDeclaration(string returnType, string name, bool isStatic)
    {
        Advance(); // skip (

        var parameters = new ParameterSyntax[16];
        int paramCount = 0;

        while (!IsAtEnd() && Current().Kind != SyntaxKind.CloseParenToken)
        {
            if (paramCount > 0)
                Expect(SyntaxKind.CommaToken);

            string paramType = ParseTypeName();
            string paramName = Current().Text;
            Advance();

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
        else if (!IsAtEnd() && Current().Kind == SyntaxKind.SemicolonToken)
        {
            // Abstract method or expression-bodied without body
            Advance();
        }

        return new MethodDeclarationSyntax(returnType, name, paramResult, body, isStatic);
    }

    private string ParseTypeName()
    {
        if (IsAtEnd()) return null;

        string name = Current().Text;
        Advance();

        // Handle array types: int[]
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBracketToken)
        {
            Advance();
            if (!IsAtEnd() && Current().Kind == SyntaxKind.CloseBracketToken)
                Advance();
            name = name + "[]";
        }

        // Handle nullable: int?
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

        if (kind == SyntaxKind.OpenBraceToken)
            return ParseBlock();

        // Try local declaration: type name = expr;
        // Heuristic: if current looks like a type (identifier/keyword) followed by an identifier
        if (IsTypeStart(kind) && _position + 1 < _tokens.Length)
        {
            SyntaxKind next = _tokens[_position + 1].Kind;
            if (next == SyntaxKind.IdentifierToken)
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

    // === Expressions (Pratt-style precedence climbing) ===

    public ExpressionSyntax ParseExpression()
    {
        return ParseAssignment();
    }

    private ExpressionSyntax ParseAssignment()
    {
        var left = ParseConditionalOr();

        if (!IsAtEnd() && Current().Kind == SyntaxKind.EqualsToken)
        {
            var op = MakeSyntaxToken(Advance());
            var right = ParseAssignment();
            return new AssignmentExpressionSyntax(8714, left, op, right);
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
        var left = ParseEquality();

        while (!IsAtEnd() && Current().Kind == SyntaxKind.AmpersandAmpersandToken)
        {
            var op = MakeSyntaxToken(Advance());
            var right = ParseEquality();
            left = new BinaryExpressionSyntax(8676, left, op, right);
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
        }

        return ParsePrimary();
    }

    private ExpressionSyntax ParsePrimary()
    {
        var expr = ParseAtom();

        // Handle postfix: member access, invocation
        while (!IsAtEnd())
        {
            if (Current().Kind == SyntaxKind.DotToken)
            {
                Advance(); // skip .
                var name = MakeSyntaxToken(Advance());
                expr = new MemberAccessExpressionSyntax(expr, name);
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

        // Parenthesized expression or cast
        if (kind == SyntaxKind.OpenParenToken)
        {
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

    private ObjectCreationExpressionSyntax ParseObjectCreation()
    {
        Advance(); // skip 'new'
        string typeName = ParseTypeName();

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

        // Skip object initializer if present: { ... }
        if (!IsAtEnd() && Current().Kind == SyntaxKind.OpenBraceToken)
            SkipBlock();

        var result = new ExpressionSyntax[argCount];
        for (int i = 0; i < argCount; i++)
            result[i] = args[i];

        return new ObjectCreationExpressionSyntax(typeName, result);
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

    // TODO: Lambda parsing — detecting `(params) => body` vs parenthesized expressions
    // is complex. The emitter already handles lambdas from the Roslyn AST, so this
    // simplified parser defers full lambda parsing to a future pass.
}
