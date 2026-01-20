using LUSharpAPI.Runtime.Internal;
using YOURPROJECT.Shared;
namespace YOURPROJECT.Server
{
    internal class ServerMain : RobloxScript
    {
        public override void GameEntry()
        {
            print("Hello server from C# in LUAU.");
            print($"Server name: {SharedModule.ServerName} version: {SharedModule.ServerVersion}");
        }
    }
}
