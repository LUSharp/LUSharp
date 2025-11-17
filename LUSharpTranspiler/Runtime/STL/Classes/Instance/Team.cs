
using System.Diagnostics.Contracts;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance
{
    public class Team : Instance
    {
        public bool AutoAssignable { get; set; }
        public BrickColor TeamColor { get; set;}

        public List<Player> GetPlayers()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal<Player> PlayerAdded { get; set; }

        public RBXScriptSignal<Player> PlayerRemoved { get; set; }
    }
}
