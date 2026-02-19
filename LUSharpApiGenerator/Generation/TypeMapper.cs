using LUSharpApiGenerator.Models;

namespace LUSharpApiGenerator.Generation;

public class TypeMapper
{
    public HashSet<string> UnmappedTypes { get; } = new();

    // Track which data types are referenced but not hand-written
    public HashSet<string> ReferencedDataTypes { get; } = new();

    // Hand-written types that already exist
    private static readonly HashSet<string> HandWrittenTypes = new()
    {
        "BrickColor", "CFrame", "Color3", "Content", "Enum", "EnumItem", "Enums",
        "Faces", "OverlapParams", "PhysicalProperties", "RBXScriptConnection",
        "Ray", "RaycastParams", "RBXScriptSignal", "RaycastResult", "Vector3"
    };

    // Hand-written classes
    private static readonly HashSet<string> HandWrittenClasses = new()
    {
        "Instance", "PVInstance", "BasePart", "Part", "Model", "SpawnLocation",
        "WorldRoot", "Player", "Mouse", "Actor", "Team", "HumanoidDescription",
        "DataModel", "BaseScript", "LuaSourceContainer", "ModuleScript", "RObject",
        "Object"
    };

    // Hand-written services
    private static readonly HashSet<string> HandWrittenServices = new()
    {
        "Players", "Workspace"
    };

    // Hand-written enums
    private static readonly HashSet<string> HandWrittenEnums = new()
    {
        "AssetType", "AvatarItemType", "Axis", "BulkMoveMode", "CameraMode",
        "CollisionFidelity", "ContentSourceType", "CreatorType",
        "DevCameraOcclusionMode", "DevComputerCameraMovementMode",
        "DevComputerMovementMode", "DevTouchCameraMovementMode",
        "DevTouchMovementMode", "IKCollisionsMode", "JoinSource",
        "MatchmakingType", "Material", "MembershipType", "ModelLevelOfDetail",
        "ModelStreamingMode", "NormalId", "PartType", "PlayerExitReason",
        "RaycastFilterType", "RenderFidelity", "RotationOrder", "RunContext",
        "SecurityCapability", "SurfaceType"
    };

    private static readonly Dictionary<string, string> PrimitiveMap = new()
    {
        ["string"] = "string",
        ["bool"] = "bool",
        ["int"] = "int",
        ["int64"] = "long",
        ["float"] = "float",
        ["double"] = "double",
        ["void"] = "void",
    };

    private static readonly Dictionary<string, string> SpecialMap = new()
    {
        ["Content"] = "Content",
        ["ProtectedString"] = "string",
        ["BinaryString"] = "byte[]",
        ["SharedString"] = "string",
        ["buffer"] = "byte[]",
        ["Variant"] = "object",
        ["Objects"] = "List<Instance>",
        ["Function"] = "LuaFunction",
        ["CoordinateFrame"] = "CFrame",
        ["OptionalCoordinateFrame"] = "CFrame?",
        ["Tuple"] = "object[]",
        ["Array"] = "object[]",
        ["Dictionary"] = "Dictionary<string, object>",
        ["QFont"] = "object",
        ["QDir"] = "object",
        ["QColor"] = "object",
        ["SecurityCapabilities"] = "long",
    };

    private static readonly HashSet<string> KnownDataTypes = new()
    {
        "Vector3", "CFrame", "Color3", "BrickColor", "Ray", "RaycastResult",
        "RaycastParams", "OverlapParams", "PhysicalProperties", "Faces",
        "Content", "RBXScriptSignal", "RBXScriptConnection",
        // These may need generation:
        "Vector2", "Vector3int16", "Vector2int16",
        "UDim", "UDim2", "Rect", "Region3", "Region3int16",
        "NumberRange", "NumberSequence", "NumberSequenceKeypoint",
        "ColorSequence", "ColorSequenceKeypoint",
        "TweenInfo", "Font", "Axes",
        "DateTime", "DockWidgetPluginGuiInfo",
        "PathWaypoint", "SharedTable",
        "FloatCurveKey", "RotationCurveKey",
        "Secret", "UniqueId",
    };

    // Set of all known class names in the dump (populated during generation)
    public HashSet<string> AllClassNames { get; } = new();

    // Set of all known enum names in the dump
    public HashSet<string> AllEnumNames { get; } = new();

    public bool IsHandWrittenClass(string name) => HandWrittenClasses.Contains(name);
    public bool IsHandWrittenService(string name) => HandWrittenServices.Contains(name);
    public bool IsHandWrittenEnum(string name) => HandWrittenEnums.Contains(name);
    public bool IsHandWrittenType(string name) => HandWrittenTypes.Contains(name);

    public string MapType(ApiValueType vt)
    {
        string name = vt.Name;
        string category = vt.Category;

        // Handle nullable primitives (e.g. "float?", "int?", "string?")
        if (name.EndsWith('?'))
        {
            string baseName = name[..^1];
            var baseType = MapType(new ApiValueType { Name = baseName, Category = category });
            return baseType + "?";
        }

        // Primitives
        if (PrimitiveMap.TryGetValue(name, out var primitive))
            return primitive;

        // Special mappings
        if (SpecialMap.TryGetValue(name, out var special))
            return special;

        // Group category (Map, Dictionary, Variant in return tuples)
        if (category == "Group")
        {
            if (name == "Map" || name == "Dictionary")
                return "Dictionary<string, object>";
            if (name == "Variant")
                return "object";
            return "object";
        }

        // Primitive "null"
        if (name == "null")
            return "object";

        // Enum types
        if (category == "Enum")
        {
            AllEnumNames.Add(name);
            return name;
        }

        // Class types
        if (category == "Class")
        {
            AllClassNames.Add(name);
            return name;
        }

        // DataType category
        if (category == "DataType")
        {
            if (KnownDataTypes.Contains(name) || HandWrittenTypes.Contains(name))
                return name;

            // Track as needing generation
            ReferencedDataTypes.Add(name);
            return name;
        }

        // Fallback
        UnmappedTypes.Add($"{category}:{name}");
        return $"object /* unmapped: {name} */";
    }

    public string MapReturnType(ApiValueType vt)
    {
        var mapped = MapType(vt);
        return mapped == "void" ? "void" : mapped;
    }

    public string MapReturnType(ApiReturnType rt)
    {
        if (rt.IsTuple)
        {
            // Tuple return: (CFrame, Vector3) → use first type for simplicity
            // C# can represent as ValueTuple
            var types = rt.Types.Select(t => MapType(t)).ToList();
            return $"({string.Join(", ", types)})";
        }
        return MapReturnType(rt.Single);
    }
}
