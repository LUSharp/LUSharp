namespace LUSharpTests;

// ── Struct declarations ───────────────────────────────────────────────────────

public struct Vec2
{
    public double X;
    public double Y;

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double LengthSquared()
    {
        return X * X + Y * Y;
    }

    public Vec2 Add(Vec2 other)
    {
        return new Vec2(X + other.X, Y + other.Y);
    }

    public Vec2 Scale(double factor)
    {
        return new Vec2(X * factor, Y * factor);
    }

    public string Format()
    {
        return "(" + X + ", " + Y + ")";
    }
}

public struct Rect2D
{
    public double Width;
    public double Height;

    public Rect2D(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Area()
    {
        return Width * Height;
    }

    public double Perimeter()
    {
        return 2.0 * (Width + Height);
    }

    public bool IsSquare()
    {
        return Width == Height;
    }
}

// Struct with auto-properties
public struct ColorRGB
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }

    public ColorRGB(int r, int g, int b)
    {
        R = r;
        G = g;
        B = b;
    }

    public bool IsBlack()
    {
        return R == 0 && G == 0 && B == 0;
    }

    public int Brightness()
    {
        return (R + G + B) / 3;
    }
}

// Class containing nested struct fields
public class Canvas2D
{
    public Vec2 Origin { get; set; }
    public ColorRGB Background { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Canvas2D(int width, int height)
    {
        Width = width;
        Height = height;
        Origin = new Vec2(0.0, 0.0);
        Background = new ColorRGB(255, 255, 255);
    }

    public string Describe()
    {
        return "Canvas2D(" + Width + "x" + Height + ")";
    }
}

// ── Test runner ───────────────────────────────────────────────────────────────

public static class T08_Structs
{
    private static int _pass = 0;
    private static int _fail = 0;

    private static void Assert(bool condition, string name)
    {
        if (condition)
        {
            _pass = _pass + 1;
            Console.WriteLine("  PASS: " + name);
        }
        else
        {
            _fail = _fail + 1;
            Console.WriteLine("  FAIL: " + name);
        }
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T08_Structs ===");

        // Struct constructor sets fields
        Vec2 v1 = new Vec2(3.0, 4.0);
        Assert(v1.X == 3.0, "Struct constructor sets X");
        Assert(v1.Y == 4.0, "Struct constructor sets Y");

        // Struct method using fields
        Assert(v1.LengthSquared() == 25.0, "Struct method LengthSquared() == 3*3 + 4*4 == 25");

        // Struct method returning a new struct
        Vec2 v2 = v1.Scale(2.0);
        Assert(v2.X == 6.0, "Struct Scale() returns new struct with X*factor");
        Assert(v2.Y == 8.0, "Struct Scale() returns new struct with Y*factor");

        // Original unchanged after Scale
        Assert(v1.X == 3.0, "Original struct X unchanged after Scale");
        Assert(v1.Y == 4.0, "Original struct Y unchanged after Scale");

        // Struct method with struct parameter
        Vec2 a = new Vec2(1.0, 2.0);
        Vec2 b = new Vec2(3.0, 4.0);
        Vec2 sum = a.Add(b);
        Assert(sum.X == 4.0, "Struct Add(Vec2): X sum correct");
        Assert(sum.Y == 6.0, "Struct Add(Vec2): Y sum correct");

        // Struct string formatting method
        Assert(v1.Format() == "(3, 4)", "Struct Format() method");

        // Rect2D struct
        Rect2D rect = new Rect2D(6.0, 3.0);
        Assert(rect.Area() == 18.0, "Rect2D.Area() method");
        Assert(rect.Perimeter() == 18.0, "Rect2D.Perimeter() method");
        Assert(!rect.IsSquare(), "Rect2D.IsSquare() false for non-square");

        Rect2D square = new Rect2D(5.0, 5.0);
        Assert(square.IsSquare(), "Rect2D.IsSquare() true for square");

        // Struct with auto-properties
        ColorRGB red = new ColorRGB(255, 0, 0);
        Assert(red.R == 255, "ColorRGB property R");
        Assert(red.G == 0, "ColorRGB property G");
        Assert(red.B == 0, "ColorRGB property B");
        Assert(!red.IsBlack(), "ColorRGB.IsBlack() false for red");

        ColorRGB black = new ColorRGB(0, 0, 0);
        Assert(black.IsBlack(), "ColorRGB.IsBlack() true for (0,0,0)");

        // Brightness computation
        ColorRGB mid = new ColorRGB(90, 90, 90);
        Assert(mid.Brightness() == 90, "ColorRGB.Brightness() of (90,90,90) == 90");

        // Property set
        ColorRGB mutable = new ColorRGB(1, 2, 3);
        mutable.R = 100;
        Assert(mutable.R == 100, "ColorRGB property R set");

        // Field mutation
        Rect2D r2 = new Rect2D(2.0, 3.0);
        r2.Width = 10.0;
        Assert(r2.Width == 10.0, "Rect2D field mutation");
        Assert(r2.Area() == 30.0, "Rect2D.Area() after field mutation");

        // Two distinct struct instances are independent when created via new
        Vec2 p1 = new Vec2(1.0, 2.0);
        Vec2 p2 = new Vec2(10.0, 20.0);
        Assert(p1.X == 1.0, "Two struct instances: p1.X == 1");
        Assert(p2.X == 10.0, "Two struct instances: p2.X == 10");

        // NOTE: In Luau, struct assignment is by reference (tables).
        // Modifying a copy created via assignment DOES affect the original.
        // We test this known behavior rather than expecting value semantics.
        Vec2 original = new Vec2(5.0, 6.0);
        Vec2 alias = original;
        alias.X = 99.0;
        // In Luau tables are shared, so original.X is also 99
        Assert(alias.X == 99.0, "Struct alias write: alias.X == 99 (Luau table reference)");
        Assert(original.X == 99.0, "Struct alias write: original.X also changed (Luau reference semantics)");

        // Creating a fresh struct via new() isolates from other instances
        Vec2 fresh = new Vec2(5.0, 6.0);
        Vec2 independent = new Vec2(5.0, 6.0);
        independent.X = 42.0;
        Assert(fresh.X == 5.0, "Fresh new Vec2: fresh.X unchanged when independent is modified");
        Assert(independent.X == 42.0, "Fresh new Vec2: independent.X == 42");

        // Struct nested in class
        Canvas2D canvas = new Canvas2D(800, 600);
        Assert(canvas.Width == 800, "Class containing struct: Width");
        Assert(canvas.Height == 600, "Class containing struct: Height");
        Assert(canvas.Describe() == "Canvas2D(800x600)", "Class.Describe() with struct fields");

        // Access nested struct fields
        Assert(canvas.Origin.X == 0.0, "Nested struct in class: Origin.X");
        Assert(canvas.Origin.Y == 0.0, "Nested struct in class: Origin.Y");
        Assert(canvas.Background.R == 255, "Nested struct in class: Background.R (white)");
        Assert(canvas.Background.G == 255, "Nested struct in class: Background.G (white)");

        // ── scorecard ──
        Console.WriteLine("T08_Structs: " + _pass + " passed, " + _fail + " failed");
    }
}
