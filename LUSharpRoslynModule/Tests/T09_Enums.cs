using System;

namespace LUSharpTests;

// ── Enum declarations ─────────────────────────────────────────────────────────

// Basic enum — auto-assigned integer values starting at 0
public enum Direction
{
    North,
    South,
    East,
    West
}

// Enum with explicit values
public enum Priority
{
    Low = 1,
    Medium = 5,
    High = 10,
    Critical = 100
}

// Enum used in switch/if chains
public enum Status
{
    Pending,
    Active,
    Inactive,
    Deleted
}

// Enum with explicit sequential values
public enum Day
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7
}

// Flags enum with bitwise values
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = 7
}

// ── Helpers ───────────────────────────────────────────────────────────────────

public static class EnumHelpers
{
    // Enum as method parameter — if-chain acts as switch
    public static string DescribeDirection(Direction d)
    {
        if (d == Direction.North) return "north";
        if (d == Direction.South) return "south";
        if (d == Direction.East) return "east";
        return "west";
    }

    public static int PriorityScore(Priority p)
    {
        if (p == Priority.Low) return 1;
        if (p == Priority.Medium) return 5;
        if (p == Priority.High) return 10;
        return 100;
    }

    public static bool IsWeekend(Day d)
    {
        return d == Day.Saturday || d == Day.Sunday;
    }

    public static string DescribeStatus(Status s)
    {
        if (s == Status.Pending) return "pending";
        if (s == Status.Active) return "active";
        if (s == Status.Inactive) return "inactive";
        return "deleted";
    }

    public static bool HasPermission(int flags, int perm)
    {
        return (flags & perm) != 0;
    }
}

// ── Test runner ───────────────────────────────────────────────────────────────

public static class T09_Enums
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
        Console.WriteLine("=== T09_Enums ===");

        // Basic enum: member identity
        Assert(Direction.North == Direction.North, "Enum identity: North == North");
        Assert(Direction.North != Direction.South, "Enum inequality: North != South");

        // Enum with explicit values: different values are unequal
        Assert(Priority.Low != Priority.High, "Explicit value enum: Low != High");
        Assert(Priority.Critical != Priority.Medium, "Explicit value enum: Critical != Medium");

        // Enum variable comparison
        Direction d = Direction.East;
        Assert(d == Direction.East, "Enum variable: d == East");
        Assert(!(d == Direction.West), "Enum variable: d != West");

        // Enum as method parameter — if-chain dispatch
        Assert(EnumHelpers.DescribeDirection(Direction.North) == "north", "Enum param: North -> north");
        Assert(EnumHelpers.DescribeDirection(Direction.West) == "west", "Enum param: West -> west");
        Assert(EnumHelpers.DescribeDirection(Direction.East) == "east", "Enum param: East -> east");
        Assert(EnumHelpers.DescribeDirection(Direction.South) == "south", "Enum param: South -> south");

        // Explicit integer values as scores
        Assert(EnumHelpers.PriorityScore(Priority.Low) == 1, "Priority.Low score == 1");
        Assert(EnumHelpers.PriorityScore(Priority.Medium) == 5, "Priority.Medium score == 5");
        Assert(EnumHelpers.PriorityScore(Priority.High) == 10, "Priority.High score == 10");
        Assert(EnumHelpers.PriorityScore(Priority.Critical) == 100, "Priority.Critical score == 100");

        // Day enum: weekend check
        Assert(EnumHelpers.IsWeekend(Day.Saturday), "Day.Saturday is weekend");
        Assert(EnumHelpers.IsWeekend(Day.Sunday), "Day.Sunday is weekend");
        Assert(!EnumHelpers.IsWeekend(Day.Monday), "Day.Monday is not weekend");
        Assert(!EnumHelpers.IsWeekend(Day.Friday), "Day.Friday is not weekend");

        // Status enum in if-chain
        Assert(EnumHelpers.DescribeStatus(Status.Pending) == "pending", "Status.Pending -> pending");
        Assert(EnumHelpers.DescribeStatus(Status.Active) == "active", "Status.Active -> active");
        Assert(EnumHelpers.DescribeStatus(Status.Deleted) == "deleted", "Status.Deleted -> deleted");

        // Default enum value: first member auto-assigned 0 → same as North
        Direction defaultDir = Direction.North;
        Assert(defaultDir == Direction.North, "Default enum value is first member");

        // Cast enum to int
        int northVal = (int)Direction.North;
        Assert(northVal == 0, "Cast Direction.North to int == 0");
        int southVal = (int)Direction.South;
        Assert(southVal == 1, "Cast Direction.South to int == 1");
        int eastVal = (int)Direction.East;
        Assert(eastVal == 2, "Cast Direction.East to int == 2");
        int westVal = (int)Direction.West;
        Assert(westVal == 3, "Cast Direction.West to int == 3");

        // Explicit-value enum: cast to int
        int lowVal = (int)Priority.Low;
        Assert(lowVal == 1, "Cast Priority.Low to int == 1");
        int highVal = (int)Priority.High;
        Assert(highVal == 10, "Cast Priority.High to int == 10");
        int critVal = (int)Priority.Critical;
        Assert(critVal == 100, "Cast Priority.Critical to int == 100");

        // Day explicit values
        int monVal = (int)Day.Monday;
        Assert(monVal == 1, "Cast Day.Monday to int == 1");
        int sunVal = (int)Day.Sunday;
        Assert(sunVal == 7, "Cast Day.Sunday to int == 7");

        // Cast int to enum
        Direction fromInt = (Direction)2;
        Assert(fromInt == Direction.East, "Cast int 2 to Direction == East");
        Direction fromInt0 = (Direction)0;
        Assert(fromInt0 == Direction.North, "Cast int 0 to Direction == North");

        // Enum.GetName — returns string name for a value
        string northName = Enum.GetName(typeof(Direction), Direction.North);
        Assert(northName == "North", "Enum.GetName(Direction.North) == \"North\"");
        string highName = Enum.GetName(typeof(Priority), Priority.High);
        Assert(highName == "High", "Enum.GetName(Priority.High) == \"High\"");
        string satName = Enum.GetName(typeof(Day), Day.Saturday);
        Assert(satName == "Saturday", "Enum.GetName(Day.Saturday) == \"Saturday\"");

        // Enum.GetValues — returns all numeric values in the enum
        int[] dirValues = (int[])Enum.GetValues(typeof(Direction));
        Assert(dirValues != null, "Enum.GetValues returns non-null array");
        Assert(dirValues.Length == 4, "Enum.GetValues(Direction) has 4 members");

        // Enum.Parse — string name → enum value
        object parsedDir = Enum.Parse(typeof(Direction), "South");
        Assert((Direction)parsedDir == Direction.South, "Enum.Parse(\"South\") == Direction.South");
        object parsedPriority = Enum.Parse(typeof(Priority), "Medium");
        Assert((Priority)parsedPriority == Priority.Medium, "Enum.Parse(\"Medium\") == Priority.Medium");

        // Flags enum: bitwise OR
        int readWrite = (int)Permissions.Read | (int)Permissions.Write;
        Assert(EnumHelpers.HasPermission(readWrite, (int)Permissions.Read), "Flags: Read|Write includes Read");
        Assert(EnumHelpers.HasPermission(readWrite, (int)Permissions.Write), "Flags: Read|Write includes Write");
        Assert(!EnumHelpers.HasPermission(readWrite, (int)Permissions.Execute), "Flags: Read|Write excludes Execute");

        // Flags: All contains all permissions
        int allPerms = (int)Permissions.All;
        Assert(EnumHelpers.HasPermission(allPerms, (int)Permissions.Read), "Flags: All includes Read");
        Assert(EnumHelpers.HasPermission(allPerms, (int)Permissions.Write), "Flags: All includes Write");
        Assert(EnumHelpers.HasPermission(allPerms, (int)Permissions.Execute), "Flags: All includes Execute");

        // Flags: None has no permissions
        int noPerms = (int)Permissions.None;
        Assert(!EnumHelpers.HasPermission(noPerms, (int)Permissions.Read), "Flags: None excludes Read");

        // Enum equality across separate variable assignments
        Status s1 = Status.Active;
        Status s2 = Status.Active;
        Assert(s1 == s2, "Two enum vars with same value are equal");
        s2 = Status.Deleted;
        Assert(s1 != s2, "Enum vars differ after reassignment");

        // Enum in arithmetic context (cast first)
        int diff = (int)Priority.High - (int)Priority.Medium;
        Assert(diff == 5, "Enum arithmetic: High - Medium == 10 - 5 == 5");

        // ── scorecard ──
        Console.WriteLine("T09_Enums: " + _pass + " passed, " + _fail + " failed");
    }
}
