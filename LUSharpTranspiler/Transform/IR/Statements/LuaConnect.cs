namespace LUSharpTranspiler.Transform.IR.Statements;

// Emits: event:Connect(function(...) ... end)
public record LuaConnect(ILuaExpression Event, ILuaExpression Handler) : ILuaStatement;
