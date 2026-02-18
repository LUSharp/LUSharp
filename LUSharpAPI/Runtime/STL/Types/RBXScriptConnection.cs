
namespace LUSharpAPI.Runtime.STL.Types
{
    public abstract class RBXScriptConnection
    {
        public bool Connected { get; internal set; }

        public abstract void Disconnect();
    }
}