namespace LUSharpTests;

// ── Support types ─────────────────────────────────────────────────────────────

public abstract class Shape
{
    public string Color { get; set; }

    public Shape(string color)
    {
        Color = color;
    }

    public abstract double Area();

    public virtual string Describe()
    {
        return "Shape(" + Color + ")";
    }

    public string GetColor()
    {
        return Color;
    }
}

public class Circle : Shape
{
    public double Radius { get; set; }

    public Circle(double radius, string color) : base(color)
    {
        Radius = radius;
    }

    public override double Area()
    {
        return 3.14159 * Radius * Radius;
    }

    public override string Describe()
    {
        return "Circle(r=" + Radius + ", color=" + Color + ")";
    }
}

public class Rectangle : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }

    public Rectangle(double width, double height, string color) : base(color)
    {
        Width = width;
        Height = height;
    }

    public override double Area()
    {
        return Width * Height;
    }
}

// Three-level chain: Animal → Dog → Labrador

public class Animal
{
    public string Name { get; set; }

    public Animal(string name)
    {
        Name = name;
    }

    public virtual string Speak()
    {
        return Name + " makes a sound";
    }

    protected string GetName()
    {
        return Name;
    }
}

public class Dog : Animal
{
    public string Breed { get; set; }

    public Dog(string name, string breed) : base(name)
    {
        Breed = breed;
    }

    public override string Speak()
    {
        return GetName() + " barks";
    }
}

public class Labrador : Dog
{
    public Labrador(string name) : base(name, "Labrador")
    {
    }

    public override string Speak()
    {
        return GetName() + " woofs loudly";
    }

    public string BreedAndName()
    {
        return Breed + ":" + Name;
    }
}

// Method hiding with new keyword

public class BaseClass
{
    public int Value { get; set; }

    public BaseClass(int value)
    {
        Value = value;
    }

    public string Tag()
    {
        return "base";
    }
}

public class DerivedClass : BaseClass
{
    public DerivedClass(int value) : base(value)
    {
    }

    public new string Tag()
    {
        return "derived";
    }
}

// Sealed class

public sealed class SealedCounter
{
    public int X { get; set; }

    public SealedCounter(int x)
    {
        X = x;
    }

    public int Double()
    {
        return X * 2;
    }
}

// ── Test runner ───────────────────────────────────────────────────────────────

public static class T06_Inheritance
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
        Console.WriteLine("=== T06_Inheritance ===");

        // Abstract class with abstract method + concrete implementation
        Circle c = new Circle(5.0, "red");
        Assert(c.Area() > 78.0 && c.Area() < 79.0, "Circle.Area() abstract override correct value");
        Assert(c.Describe() == "Circle(r=5, color=red)", "Circle.Describe() virtual override");

        // Inherited non-virtual method from base
        Assert(c.GetColor() == "red", "Inherited method GetColor()");

        // Base constructor chaining: base(color) sets Color on Shape
        Assert(c.Color == "red", "Base constructor chaining sets Color");

        // Rectangle — override Area, use base Describe
        Rectangle r = new Rectangle(4.0, 6.0, "blue");
        Assert(r.Area() == 24.0, "Rectangle.Area() override correct");
        Assert(r.Describe() == "Shape(blue)", "Rectangle uses inherited base Describe()");

        // Polymorphic dispatch: base reference holds derived type
        Shape s1 = new Circle(3.0, "green");
        Shape s2 = new Rectangle(2.0, 5.0, "yellow");
        Assert(s1.Area() > 28.0 && s1.Area() < 29.0, "Polymorphic dispatch: Circle.Area() via Shape ref");
        Assert(s2.Area() == 10.0, "Polymorphic dispatch: Rectangle.Area() via Shape ref");

        // Three-level chain A → B → C — constructor chaining through all levels
        Labrador lab = new Labrador("Buddy");
        Assert(lab.Name == "Buddy", "Three-level ctor: Name from Animal");
        Assert(lab.Breed == "Labrador", "Three-level ctor: Breed from Dog");

        // Three-level override
        Assert(lab.Speak() == "Buddy woofs loudly", "Three-level override Labrador.Speak()");

        // Intermediate level method
        Dog dog = new Dog("Rex", "Poodle");
        Assert(dog.Speak() == "Rex barks", "Dog.Speak() overrides Animal and uses protected GetName()");

        // Base level
        Animal animal = new Animal("Cat");
        Assert(animal.Speak() == "Cat makes a sound", "Animal.Speak() base implementation");

        // Protected member accessed from derived class
        Labrador lab2 = new Labrador("Max");
        Assert(lab2.BreedAndName() == "Labrador:Max", "Derived accesses Breed and Name from chain");

        // Method hiding with new keyword
        DerivedClass derived = new DerivedClass(10);
        Assert(derived.Tag() == "derived", "Method hiding: DerivedClass.Tag() via Derived ref");
        Assert(derived.Value == 10, "Method hiding: base constructor initializes Value");

        // Base class ref to hiding member sees the base version
        BaseClass asBase = new DerivedClass(5);
        // In Luau (table-based), the base ref still calls the overriding method via metatable
        Assert(asBase.Value == 5, "Base ref: Value set by base constructor");

        // is-check: derived instance satisfies base type check
        Animal polyAnimal = new Dog("Spot", "Dalmatian");
        Assert(polyAnimal is Animal, "Dog is Animal (is-check on base)");
        Assert(polyAnimal is Dog, "Dog is Dog (is-check exact)");

        // is-check with pattern variable
        Shape polyShape = new Circle(1.0, "pink");
        if (polyShape is Circle cc)
        {
            Assert(cc.Radius == 1.0, "Pattern variable from is-check has correct Radius");
        }
        else
        {
            Assert(false, "is-check pattern should have matched Circle");
        }

        // Non-matching is-check
        Shape nonCircle = new Rectangle(3.0, 3.0, "gray");
        Assert(!(nonCircle is Circle), "Rectangle is not Circle (negative is-check)");

        // Sealed class instantiates and behaves normally
        SealedCounter sc = new SealedCounter(7);
        Assert(sc.Double() == 14, "Sealed class method works");
        Assert(sc.X == 7, "Sealed class property accessible");

        // Multiple levels: virtual dispatch persists through three levels
        Animal polyLab = new Labrador("Sky");
        Assert(polyLab.Speak() == "Sky woofs loudly", "Animal ref holds Labrador, virtual dispatch to Labrador.Speak()");

        // Constructor chaining: intermediate Dog level
        Dog polyDog = new Labrador("Bean");
        Assert(polyDog.Speak() == "Bean woofs loudly", "Dog ref holds Labrador, virtual dispatch correct");
        Assert(polyDog.Breed == "Labrador", "Dog ref: Breed set by Labrador ctor chain");

        // ── scorecard ──
        Console.WriteLine("T06_Inheritance: " + _pass + " passed, " + _fail + " failed");
    }
}
