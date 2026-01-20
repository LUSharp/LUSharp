using LUSharpAPI.Runtime.Internal;
using LUSharpAPI.Runtime.STL.Classes.Instance.LuaSourceContainer;
namespace YOURPROJECT.Shared
{
    public class SharedModule : ModuleScript
    {
        public static string ServerName { get; set; } = "TestServer";
        public static string ServerVersion { get; set; } = "v1.0.0";
    }
}
