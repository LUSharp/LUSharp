
using LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance
{
    public class Actor : Model
    {
        public RBXScriptSignal<string, Action<string>> BindToMessage{ get; set; }
        public RBXScriptSignal<string, Action<string>> BindToMessageParallel{ get; set; }
        public void SendMessage(string topic, params string[] message)
        {
            throw new NotImplementedException();
        }
    }
}