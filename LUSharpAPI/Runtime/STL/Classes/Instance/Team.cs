
using System.Diagnostics.Contracts;
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes.Instance
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
