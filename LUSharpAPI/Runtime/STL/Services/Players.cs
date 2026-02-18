
using LUSharpAPI.Runtime.STL.Classes;
using LUSharpAPI.Runtime.STL.Classes.Instance;
using LUSharpAPI.Runtime.STL.Enums;
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Services
{
    public class Players : Instance
    {
        /// <summary>
        /// Indicates whether or not bubble chat is enabled. It is set with the Players:SetChatStyle() method.
        /// </summary>
        public bool BubbleChat{ get; set; }
        /// <summary>
        /// Indicates whether characters will respawn automatically.
        /// </summary>
        public bool CharacterAutoLoads { get; set; }
        /// <summary>
        /// Indicates whether or not classic chat is enabled; set by the Players:SetChatStyle() method.
        /// </summary>
        public bool ClassicChat { get; set; }
        /// <summary>
        /// The Player that the LocalScript is running for.
        /// </summary>
        public Player LocalPlayer {get;set;}
        /// <summary>
        /// The maximum number of players that can be in a server.
        /// </summary>
        public double MaxPlayers { get; set; }
        /// <summary>
        /// The preferred number of players for a server.
        /// </summary>
        public double PreferredPlayers { get; set; }
        /// <summary>
        /// Controls the amount of time taken for a players character to respawn.
        /// </summary>
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