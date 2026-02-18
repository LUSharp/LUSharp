
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance
{
    public class SpawnLocation : Part
    {
        public bool AllowTeamChangeOnTouch { get; set; }
        public double Duration{ get; set; }
        public bool Enabled { get; set; }
        public bool Neutral { get; set; }
        public BrickColor TeamColor { get; set; }
    }
}