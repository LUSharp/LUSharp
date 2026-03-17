namespace LUSharpTests;

// ── Interface declarations ────────────────────────────────────────────────────

public interface IDescribable
{
    string Describe();
}

public interface IResizable
{
    void Resize(double factor);
    double GetSize();
}

public interface IColorable
{
    string GetColor();
    void SetColor(string color);
}

// Marker interface (empty — no members)
public interface IMarker
{
}

// Interface with property
public interface INamed
{
    string Name { get; set; }
}

// ── Classes implementing interfaces ──────────────────────────────────────────

// Single interface
public class BoxShape : IDescribable
{
    public double Side { get; set; }

    public BoxShape(double side)
    {
        Side = side;
    }

    public string Describe()
    {
        return "Box(side=" + Side + ")";
    }
}

// Multiple interface implementation
public class Widget : IDescribable, IResizable, IColorable, IMarker
{
    public double Size { get; set; }
    private string _color;

    public Widget(double size, string color)
    {
        Size = size;
        _color = color;
    }

    public string Describe()
    {
        return "Widget(size=" + Size + ", color=" + _color + ")";
    }

    public void Resize(double factor)
    {
        Size = Size * factor;
    }

    public double GetSize()
    {
        return Size;
    }

    public string GetColor()
    {
        return _color;
    }

    public void SetColor(string color)
    {
        _color = color;
    }
}

// Class implementing interface with property
public class NamedPlayer : INamed
{
    public string Name { get; set; }
    public int Score { get; set; }

    public NamedPlayer(string name, int score)
    {
        Name = name;
        Score = score;
    }
}

// Interface as parameter and return type helpers
public static class InterfaceUtils
{
    public static string CallDescribe(IDescribable d)
    {
        return d.Describe();
    }

    public static double CallGetSize(IResizable r)
    {
        return r.GetSize();
    }

    public static IDescribable MakeBox(double side)
    {
        return new BoxShape(side);
    }

    public static void CallResize(IResizable r, double factor)
    {
        r.Resize(factor);
    }
}

// ── Test runner ───────────────────────────────────────────────────────────────

public static class T07_Interfaces
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
        Console.WriteLine("=== T07_Interfaces ===");

        // Single interface implementation — direct call
        BoxShape box = new BoxShape(4.0);
        Assert(box.Describe() == "Box(side=4)", "BoxShape.Describe() direct call");

        // Multiple interface implementation — all methods callable
        Widget w = new Widget(10.0, "blue");
        Assert(w.Describe() == "Widget(size=10, color=blue)", "Widget.Describe() multi-interface");
        Assert(w.GetSize() == 10.0, "Widget.GetSize() from IResizable");
        Assert(w.GetColor() == "blue", "Widget.GetColor() from IColorable");

        // IResizable method mutates state
        w.Resize(2.0);
        Assert(w.GetSize() == 20.0, "Widget.Resize(2.0) doubles Size");
        Assert(w.Size == 20.0, "Widget.Size field updated after Resize");

        // IColorable setter
        w.SetColor("red");
        Assert(w.GetColor() == "red", "Widget.SetColor mutates color");

        // Interface as parameter type: dispatch through interface
        BoxShape box2 = new BoxShape(7.0);
        string r1 = InterfaceUtils.CallDescribe(box2);
        Assert(r1 == "Box(side=7)", "Interface param IDescribable: CallDescribe(BoxShape)");

        Widget w2 = new Widget(5.0, "green");
        double sz = InterfaceUtils.CallGetSize(w2);
        Assert(sz == 5.0, "Interface param IResizable: CallGetSize(Widget)");

        // Resize via interface parameter
        Widget w3 = new Widget(8.0, "black");
        InterfaceUtils.CallResize(w3, 0.5);
        Assert(w3.Size == 4.0, "CallResize via IResizable param halves size");

        // Interface as return type
        IDescribable returned = InterfaceUtils.MakeBox(3.0);
        Assert(returned.Describe() == "Box(side=3)", "Interface return type: MakeBox() dispatches Describe");

        // is-check: class satisfies interface check
        Widget w4 = new Widget(1.0, "white");
        Assert(w4 is IDescribable, "Widget is IDescribable");
        Assert(w4 is IResizable, "Widget is IResizable");
        Assert(w4 is IColorable, "Widget is IColorable");
        Assert(w4 is IMarker, "Widget is IMarker (empty interface)");

        // Negative is-check: class not implementing interface
        BoxShape nonResizable = new BoxShape(2.0);
        Assert(!(nonResizable is IResizable), "BoxShape is not IResizable");
        Assert(!(nonResizable is IColorable), "BoxShape is not IColorable");

        // Interface with property: get and set via concrete class
        NamedPlayer p = new NamedPlayer("Alice", 100);
        Assert(p.Name == "Alice", "INamed.Name property get");
        p.Name = "Bob";
        Assert(p.Name == "Bob", "INamed.Name property set via interface property");
        Assert(p.Score == 100, "Non-interface field accessible alongside interface");

        // Empty marker interface — class instantiates and methods work
        Widget marker = new Widget(99.0, "gold");
        Assert(marker.Describe() == "Widget(size=99, color=gold)", "Marker interface class still usable");

        // Virtual dispatch through interface reference
        IDescribable iDesc = new Widget(6.0, "cyan");
        Assert(iDesc.Describe() == "Widget(size=6, color=cyan)", "IDescribable ref dispatches to Widget.Describe");

        // Interface dispatch through IResizable ref
        IResizable iRes = new Widget(12.0, "purple");
        Assert(iRes.GetSize() == 12.0, "IResizable ref dispatches GetSize");
        iRes.Resize(3.0);
        Assert(iRes.GetSize() == 36.0, "IResizable ref dispatches Resize, GetSize reflects change");

        // ── scorecard ──
        Console.WriteLine("T07_Interfaces: " + _pass + " passed, " + _fail + " failed");
    }
}
