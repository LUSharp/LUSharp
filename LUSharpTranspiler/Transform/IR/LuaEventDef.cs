namespace LUSharpTranspiler.Transform.IR;

// Custom C# event → BindableEvent-backed Lua event
public record LuaEventDef(string Name, string SignatureType);
