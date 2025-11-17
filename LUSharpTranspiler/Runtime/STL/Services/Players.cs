
using LUSharpTranspiler.Runtime.STL.Classes;
using LUSharpTranspiler.Runtime.STL.Classes.Instance;
using LUSharpTranspiler.Runtime.STL.Enums;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Services
{
    public class Players : Instance
    {
        public bool BubbleChat{ get; set; }
        public bool CharacterAutoLoads { get; set; }
        public bool ClassicChat { get; set; }
        public Player LocalPlayer {get;set;}
        public double MaxPlayers { get; set; }
        public double PreferredPlayers { get; set; }
        public double RespawnTime { get; set; }
        

        public void BanAsync((List<double> UserIds, bool ApplyToUniverse, double Duration, string DisplayReason, string PrivateReason, bool ExcludeAltAccounts) config)
        {
            throw new NotImplementedException();
        }

        public List<Player> GetPlayers()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal<Player> PlayerAdded { get; set; }

        public RBXScriptSignal<Player, PlayerExitReason> PlayerRemoving { get; set; }
    }
}