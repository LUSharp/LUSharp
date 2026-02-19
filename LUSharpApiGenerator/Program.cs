using System.Text.Json;
using LUSharpApiGenerator.Models;

namespace LUSharpApiGenerator;

public class Program
{
    private const string ApiDumpUrl =
        "https://raw.githubusercontent.com/MaximumADHD/Roblox-Client-Tracker/roblox/Full-API-Dump.json";

    private static readonly string CacheDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "cache");

    private static readonly string CachePath =
        Path.Combine(CacheDir, "Full-API-Dump.json");

    public static async Task Main(string[] args)
    {
        bool forceFetch = args.Contains("--force-fetch");

        Console.WriteLine("=== LUSharp API Generator ===");
        Console.WriteLine();

        // Step 1: Fetch or load cached API dump
        string json = await FetchOrLoadAsync(forceFetch);

        // Step 2: Deserialize
        Console.WriteLine("Deserializing API dump...");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new ApiMemberConverter());
        var dump = JsonSerializer.Deserialize<ApiDump>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize API dump.");

        Console.WriteLine($"  Classes: {dump.Classes.Count}");
        Console.WriteLine($"  Enums:   {dump.Enums.Count}");
        Console.WriteLine();

        // Step 3: Filter
        var filter = new Filtering.ApiFilter();
        var (filteredClasses, filteredEnums) = filter.Filter(dump);

        Console.WriteLine($"After filtering:");
        Console.WriteLine($"  Classes: {filteredClasses.Count}");
        Console.WriteLine($"  Enums:   {filteredEnums.Count}");
        Console.WriteLine();

        // Step 4: Resolve inheritance
        var resolver = new Generation.InheritanceResolver();
        resolver.BuildTree(filteredClasses);

        // Step 5: Generate
        string apiProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LUSharpAPI"));

        var typeMapper = new Generation.TypeMapper();
        var naming = new Generation.CSharpNaming();

        // Clean generated directories
        string generatedRoot = Path.Combine(apiProjectRoot, "Runtime", "STL", "Generated");
        if (Directory.Exists(generatedRoot))
            Directory.Delete(generatedRoot, true);

        var enumGen = new Generation.EnumGenerator(naming);
        var classGen = new Generation.ClassGenerator(typeMapper, naming, resolver);
        var dataTypeGen = new Generation.DataTypeGenerator(typeMapper, naming);

        int enumCount = enumGen.Generate(filteredEnums, apiProjectRoot);
        var (classCount, serviceCount) = classGen.Generate(filteredClasses, apiProjectRoot);
        int dataTypeCount = dataTypeGen.Generate(filteredClasses, apiProjectRoot);

        Console.WriteLine($"Generated:");
        Console.WriteLine($"  Enums:      {enumCount}");
        Console.WriteLine($"  Classes:    {classCount}");
        Console.WriteLine($"  Services:   {serviceCount}");
        Console.WriteLine($"  DataTypes:  {dataTypeCount}");
        Console.WriteLine($"  Unmapped:   {typeMapper.UnmappedTypes.Count}");

        if (typeMapper.UnmappedTypes.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Unmapped types:");
            foreach (var t in typeMapper.UnmappedTypes.OrderBy(t => t))
                Console.WriteLine($"  - {t}");
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    private static async Task<string> FetchOrLoadAsync(bool forceFetch)
    {
        if (!forceFetch && File.Exists(CachePath))
        {
            Console.WriteLine($"Using cached API dump: {CachePath}");
            return await File.ReadAllTextAsync(CachePath);
        }

        Console.WriteLine($"Downloading API dump from {ApiDumpUrl}...");
        using var http = new HttpClient();
        string json = await http.GetStringAsync(ApiDumpUrl);

        Directory.CreateDirectory(CacheDir);
        await File.WriteAllTextAsync(CachePath, json);
        Console.WriteLine($"Cached to: {CachePath}");

        return json;
    }
}
