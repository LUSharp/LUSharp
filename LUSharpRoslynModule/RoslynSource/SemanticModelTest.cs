using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticModelTest;

public class Player
{
    public string Name { get; set; }
    public int Health { get; set; }
    public List<string> Inventory { get; set; }

    public Player(string name, int health)
    {
        Name = name;
        Health = health;
        Inventory = new List<string>();
    }

    // String concatenation (SemanticModel detects string type)
    public string GetStatus()
    {
        return Name + " has " + Health + " HP";
    }

    // Interpolated string
    public string GetStatusInterpolated()
    {
        return $"Player {Name} has {Health} HP";
    }

    // Collection methods (MethodMapper)
    public void AddItem(string item)
    {
        Inventory.Add(item);
    }

    public bool HasItem(string item)
    {
        return Inventory.Contains(item);
    }

    public int FindItem(string item)
    {
        return Inventory.IndexOf(item);
    }

    // String methods (MethodMapper)
    public string GetLowerName()
    {
        return Name.ToLower();
    }

    public bool NameContains(string sub)
    {
        return Name.Contains(sub);
    }

    public string GetNameSubstring(int start, int length)
    {
        return Name.Substring(start, length);
    }

    // is/as pattern matching
    public string Describe(object obj)
    {
        if (obj is string s)
        {
            return $"String: {s}";
        }
        if (obj is int n)
        {
            return $"Number: {n}";
        }
        if (obj is Player p)
        {
            return $"Player: {p.Name}";
        }
        return "Unknown";
    }

    // typeof
    public string GetTypeName()
    {
        return typeof(Player).ToString();
    }

    // Object initializer
    public static Player CreateDefault()
    {
        return new Player("Default", 100) { Name = "Hero" };
    }

    // Math operations
    public int ClampHealth(int min, int max)
    {
        return Math.Clamp(Health, min, max);
    }

    // StringBuilder
    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.Append("Report for ");
        sb.Append(Name);
        sb.AppendLine();
        sb.Append("Health: ");
        sb.Append(Health);
        return sb.ToString();
    }

    // Null coalescing
    public string SafeName(string? fallback)
    {
        return Name ?? fallback ?? "Unknown";
    }

    // Using statement
    public void ProcessWithUsing()
    {
        Console.WriteLine("Processing");
    }

    // Switch expression
    public string HealthCategory()
    {
        return Health switch
        {
            > 80 => "Healthy",
            > 50 => "Injured",
            > 20 => "Critical",
            _ => "Dead"
        };
    }

    // Local function
    public int ComputeScore()
    {
        int bonus(int level)
        {
            return level * 10;
        }
        return Health + bonus(5);
    }

    // Dictionary usage
    public static Dictionary<string, int> GetScores()
    {
        var scores = new Dictionary<string, int>();
        scores["Alice"] = 100;
        scores["Bob"] = 85;
        if (scores.ContainsKey("Alice"))
        {
            Console.WriteLine("Alice found");
        }
        return scores;
    }

    // Array operations
    public void SortInventory()
    {
        Inventory.Sort();
        var count = Inventory.Count;
        Console.WriteLine($"Sorted {count} items");
    }

    // Conditional access
    public int? GetNameLength()
    {
        return Name?.Length;
    }
}
