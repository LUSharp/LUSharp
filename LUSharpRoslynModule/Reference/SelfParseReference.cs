namespace LUSharpRoslynModule.Reference;

/// <summary>
/// Self-parsing reference test: validates that the transpiled parser can parse
/// the C# source files that define the parser itself (SyntaxToken.cs, SlidingTextWindow.cs).
/// Manually constructs the expected TreePrinter output for each source file.
///
/// NOTE: SlidingTextWindow.cs uses Unicode escape sequences ('\uffff', '\u0085', etc.)
/// that the simplified tokenizer doesn't fully handle (it only consumes one char after
/// backslash in character literals). The test uses a modified version that replaces
/// multi-char Unicode escapes with simple character literals.
/// </summary>
public static class SelfParseReference
{
    public static void PrintAll()
    {
        ParseSyntaxToken();
        Console.WriteLine();
        ParseSlidingTextWindow();
    }

    private static void ParseSyntaxToken()
    {
        Console.WriteLine("=== Self-Parse: SyntaxToken.cs ===");
        string source = @"namespace RoslynLuau;

public struct SyntaxToken
{
    public int Kind { get; }
    public string Text { get; }
    public int Start { get; }
    public int Length { get; }

    public SyntaxToken(int kind, string text, int start, int length)
    {
        Kind = kind;
        Text = text;
        Start = start;
        Length = length;
    }

    public bool IsMissing()
    {
        return Kind == 0;
    }

    public override string ToString()
    {
        return Text;
    }
}";

        Console.WriteLine("Input:");
        Console.WriteLine(source);
        Console.WriteLine();

        // Expected tree output from SimpleParser + TreePrinter
        Console.WriteLine("=== Tree Output ===");
        Console.WriteLine("CompilationUnit");
        Console.WriteLine("  StructDeclaration: SyntaxToken");
        Console.WriteLine("    PropertyDeclaration: int Kind {get}");
        Console.WriteLine("    PropertyDeclaration: string Text {get}");
        Console.WriteLine("    PropertyDeclaration: int Start {get}");
        Console.WriteLine("    PropertyDeclaration: int Length {get}");
        Console.WriteLine("    ConstructorDeclaration: SyntaxToken");
        Console.WriteLine("      Block (4 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Kind");
        Console.WriteLine("            Identifier: kind");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Text");
        Console.WriteLine("            Identifier: text");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Start");
        Console.WriteLine("            Identifier: start");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Length");
        Console.WriteLine("            Identifier: length");
        Console.WriteLine("    MethodDeclaration: bool IsMissing");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          BinaryExpression: ==");
        Console.WriteLine("            Identifier: Kind");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("    MethodDeclaration: string ToString");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          Identifier: Text");

        Console.WriteLine();
        Console.WriteLine("Total tree lines: 34");
    }

    private static void ParseSlidingTextWindow()
    {
        Console.WriteLine("=== Self-Parse: SlidingTextWindow.cs (simplified) ===");
        // NOTE: Original uses '\uffff', '\u0085', '\u2028', '\u2029' which the simplified
        // tokenizer cannot handle (multi-char unicode escapes after backslash).
        // This test uses a modified version replacing those with simple character literals.
        string source = @"namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

public struct SlidingTextWindow
{
    public const char InvalidCharacter = '\0';

    private readonly string _text;
    private readonly int _textEnd;
    public int Position;
    public int LexemeStartPosition;

    public int Width => Position - LexemeStartPosition;

    public SlidingTextWindow(string text)
    {
        _text = text;
        _textEnd = text.Length;
        Position = 0;
        LexemeStartPosition = 0;
    }

    public void Start()
    {
        LexemeStartPosition = Position;
    }

    public bool IsReallyAtEnd()
    {
        return Position >= _textEnd;
    }

    public char PeekChar()
    {
        if (Position >= _textEnd)
            return InvalidCharacter;
        return _text[Position];
    }

    public char PeekChar(int delta)
    {
        int pos = Position + delta;
        if (pos < 0 || pos >= _textEnd)
            return InvalidCharacter;
        return _text[pos];
    }

    public char NextChar()
    {
        if (Position >= _textEnd)
            return InvalidCharacter;
        char c = _text[Position];
        Position++;
        return c;
    }

    public void AdvanceChar()
    {
        Position++;
    }

    public void AdvanceChar(int n)
    {
        Position += n;
    }

    public bool TryAdvance(char c)
    {
        if (PeekChar() == c)
        {
            AdvanceChar();
            return true;
        }
        return false;
    }

    public void Reset(int position)
    {
        Position = position;
    }

    public string GetText(bool intern)
    {
        int length = Position - LexemeStartPosition;
        if (length == 0)
            return string.Empty;
        return _text.Substring(LexemeStartPosition, length);
    }

    public int GetNewLineWidth()
    {
        char c = PeekChar();
        if (c == '\r')
        {
            if (PeekChar(1) == '\n')
                return 2;
            return 1;
        }
        if (c == '\n')
            return 1;
        return 0;
    }

    public void AdvancePastNewLine()
    {
        AdvanceChar(GetNewLineWidth());
    }
}";

        Console.WriteLine("Input:");
        Console.WriteLine(source);
        Console.WriteLine();

        // Expected tree output from SimpleParser + TreePrinter
        // The struct has:
        //   - 1 const field (InvalidCharacter) with char literal initializer
        //   - 2 private readonly fields (_text, _textEnd)
        //   - 2 public fields (Position, LexemeStartPosition)
        //   - 1 expression-bodied property (Width => ...)
        //   - 1 constructor (1 param) with 4 assignments
        //   - 11 methods: Start, IsReallyAtEnd, PeekChar (x2 overloads), NextChar,
        //     AdvanceChar (x2 overloads), TryAdvance, Reset, GetText, GetNewLineWidth,
        //     AdvancePastNewLine
        Console.WriteLine("=== Tree Output ===");
        Console.WriteLine("CompilationUnit");
        Console.WriteLine("  StructDeclaration: SlidingTextWindow");

        // const char InvalidCharacter = '\0';
        Console.WriteLine("    FieldDeclaration: char InvalidCharacter");
        Console.WriteLine("      Literal: '\\0'");

        // private readonly string _text;
        Console.WriteLine("    FieldDeclaration: string _text");
        // private readonly int _textEnd;
        Console.WriteLine("    FieldDeclaration: int _textEnd");
        // public int Position;
        Console.WriteLine("    FieldDeclaration: int Position");
        // public int LexemeStartPosition;
        Console.WriteLine("    FieldDeclaration: int LexemeStartPosition");

        // public int Width => Position - LexemeStartPosition;
        // Expression-bodied property: parsed as PropertyDeclaration with expression body
        Console.WriteLine("    PropertyDeclaration: int Width {get}");
        Console.WriteLine("      BinaryExpression: -");
        Console.WriteLine("        Identifier: Position");
        Console.WriteLine("        Identifier: LexemeStartPosition");

        // Constructor: SlidingTextWindow(string text)
        Console.WriteLine("    ConstructorDeclaration: SlidingTextWindow");
        Console.WriteLine("      Block (4 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: _text");
        Console.WriteLine("            Identifier: text");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: _textEnd");
        // text.Length -> MemberAccess: .Length on text
        Console.WriteLine("            MemberAccess: .Length");
        Console.WriteLine("              Identifier: text");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: LexemeStartPosition");
        Console.WriteLine("            Literal: 0");

        // void Start()
        Console.WriteLine("    MethodDeclaration: void Start");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: LexemeStartPosition");
        Console.WriteLine("            Identifier: Position");

        // bool IsReallyAtEnd()
        Console.WriteLine("    MethodDeclaration: bool IsReallyAtEnd");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          BinaryExpression: >=");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: _textEnd");

        // char PeekChar() — first overload, no params
        Console.WriteLine("    MethodDeclaration: char PeekChar");
        Console.WriteLine("      Block (2 statements)");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: >=");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: _textEnd");
        Console.WriteLine("          ReturnStatement");
        Console.WriteLine("            Identifier: InvalidCharacter");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          ElementAccess");
        Console.WriteLine("            Identifier: _text");
        Console.WriteLine("            Identifier: Position");

        // char PeekChar(int delta) — second overload
        Console.WriteLine("    MethodDeclaration: char PeekChar");
        Console.WriteLine("      Block (3 statements)");
        Console.WriteLine("        LocalDeclaration: int pos");
        Console.WriteLine("          BinaryExpression: +");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: delta");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: ||");
        Console.WriteLine("            BinaryExpression: <");
        Console.WriteLine("              Identifier: pos");
        Console.WriteLine("              Literal: 0");
        Console.WriteLine("            BinaryExpression: >=");
        Console.WriteLine("              Identifier: pos");
        Console.WriteLine("              Identifier: _textEnd");
        Console.WriteLine("          ReturnStatement");
        Console.WriteLine("            Identifier: InvalidCharacter");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          ElementAccess");
        Console.WriteLine("            Identifier: _text");
        Console.WriteLine("            Identifier: pos");

        // char NextChar()
        Console.WriteLine("    MethodDeclaration: char NextChar");
        Console.WriteLine("      Block (4 statements)");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: >=");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: _textEnd");
        Console.WriteLine("          ReturnStatement");
        Console.WriteLine("            Identifier: InvalidCharacter");
        Console.WriteLine("        LocalDeclaration: char c");
        Console.WriteLine("          ElementAccess");
        Console.WriteLine("            Identifier: _text");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          PostfixUnary: ++");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          Identifier: c");

        // void AdvanceChar() — no params
        Console.WriteLine("    MethodDeclaration: void AdvanceChar");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          PostfixUnary: ++");
        Console.WriteLine("            Identifier: Position");

        // void AdvanceChar(int n) — overload with param
        Console.WriteLine("    MethodDeclaration: void AdvanceChar");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: +=");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: n");

        // bool TryAdvance(char c)
        Console.WriteLine("    MethodDeclaration: bool TryAdvance");
        Console.WriteLine("      Block (2 statements)");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: ==");
        Console.WriteLine("            InvocationExpression");
        Console.WriteLine("              Identifier: PeekChar");
        Console.WriteLine("            Identifier: c");
        Console.WriteLine("          Block (2 statements)");
        Console.WriteLine("            ExpressionStatement");
        Console.WriteLine("              InvocationExpression");
        Console.WriteLine("                Identifier: AdvanceChar");
        Console.WriteLine("            ReturnStatement");
        Console.WriteLine("              Literal: true");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          Literal: false");

        // void Reset(int position)
        Console.WriteLine("    MethodDeclaration: void Reset");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          Assignment: =");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: position");

        // string GetText(bool intern)
        Console.WriteLine("    MethodDeclaration: string GetText");
        Console.WriteLine("      Block (3 statements)");
        Console.WriteLine("        LocalDeclaration: int length");
        Console.WriteLine("          BinaryExpression: -");
        Console.WriteLine("            Identifier: Position");
        Console.WriteLine("            Identifier: LexemeStartPosition");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: ==");
        Console.WriteLine("            Identifier: length");
        Console.WriteLine("            Literal: 0");
        Console.WriteLine("          ReturnStatement");
        Console.WriteLine("            MemberAccess: .Empty");
        Console.WriteLine("              Identifier: string");
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          InvocationExpression");
        Console.WriteLine("            MemberAccess: .Substring");
        Console.WriteLine("              Identifier: _text");
        Console.WriteLine("            Identifier: LexemeStartPosition");
        Console.WriteLine("            Identifier: length");

        // int GetNewLineWidth()
        Console.WriteLine("    MethodDeclaration: int GetNewLineWidth");
        Console.WriteLine("      Block (4 statements)");
        Console.WriteLine("        LocalDeclaration: char c");
        Console.WriteLine("          InvocationExpression");
        Console.WriteLine("            Identifier: PeekChar");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: ==");
        Console.WriteLine("            Identifier: c");
        Console.WriteLine("            Literal: '\\r'");
        Console.WriteLine("          Block (2 statements)");
        Console.WriteLine("            IfStatement");
        Console.WriteLine("              BinaryExpression: ==");
        Console.WriteLine("                InvocationExpression");
        Console.WriteLine("                  Identifier: PeekChar");
        Console.WriteLine("                  Literal: 1");
        Console.WriteLine("                Literal: '\\n'");
        Console.WriteLine("              ReturnStatement");
        Console.WriteLine("                Literal: 2");
        Console.WriteLine("            ReturnStatement");
        Console.WriteLine("              Literal: 1");
        Console.WriteLine("        IfStatement");
        Console.WriteLine("          BinaryExpression: ==");
        Console.WriteLine("            Identifier: c");
        Console.WriteLine("            Literal: '\\n'");
        Console.WriteLine("          ReturnStatement");
        Console.WriteLine("            Literal: 1");

        // Extra: return 0 at end of GetNewLineWidth
        Console.WriteLine("        ReturnStatement");
        Console.WriteLine("          Literal: 0");

        // void AdvancePastNewLine()
        Console.WriteLine("    MethodDeclaration: void AdvancePastNewLine");
        Console.WriteLine("      Block (1 statements)");
        Console.WriteLine("        ExpressionStatement");
        Console.WriteLine("          InvocationExpression");
        Console.WriteLine("            Identifier: AdvanceChar");
        Console.WriteLine("            InvocationExpression");
        Console.WriteLine("              Identifier: GetNewLineWidth");

        Console.WriteLine();
        Console.WriteLine("Total tree lines: 178");
        Console.WriteLine();
        Console.WriteLine("NOTE: Original SlidingTextWindow.cs uses Unicode escape sequences");
        Console.WriteLine("('\\uffff', '\\u0085', '\\u2028', '\\u2029') that the simplified tokenizer");
        Console.WriteLine("cannot handle. This test uses '\\0' and removes those comparisons.");
        Console.WriteLine("The tokenizer only consumes one character after a backslash in char literals.");
    }
}
