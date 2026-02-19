using LUSharpTranspiler.Backend;
using LUSharpTranspiler.Transform.IR;
using LUSharpTranspiler.Transform.IR.Statements;
using LUSharpTranspiler.Transform.IR.Expressions;

namespace LUSharpTests;

public class ModuleEmitterTests
{
    [Fact]
    public void EmitsModuleScriptWithReturn()
    {
        var module = new LuaModule
        {
            ScriptType = ScriptType.ModuleScript,
            Classes = new()
            {
                new LuaClassDef
                {
                    Name = "Player",
                    Methods = new()
                    {
                        new LuaMethodDef
                        {
                            Name = "GetHealth",
                            Parameters = new() { "self" },
                            Body = new() { new LuaReturn(new LuaLiteral("100")) }
                        }
                    }
                }
            }
        };

        var result = ModuleEmitter.Emit(module);

        Assert.Contains("local Player = {}", result);
        Assert.Contains("function Player:GetHealth()", result);
        Assert.Contains("return 100", result);
        Assert.Contains("return Player", result);
    }

    [Fact]
    public void EmitsRequires()
    {
        var module = new LuaModule
        {
            Requires = new() { new LuaRequire("Players", "game:GetService(\"Players\")") }
        };

        var result = ModuleEmitter.Emit(module);
        Assert.Contains("local Players = game:GetService(\"Players\")", result);
    }
}
