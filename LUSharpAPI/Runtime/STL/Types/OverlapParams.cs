
using LUSharpAPI.Runtime.STL.Classes;

namespace LUSharpAPI.Runtime.STL.Enums
{
    public class OverlapParams
    {
        public List<RObject> FilterDescendantsInstances{ get; set; }
        public RaycastFilterType FilterType{ get; set; }
        public double MaxParts{ get; set; }
        public string CollisionGroup{ get; set; }
        public double Tolorance{ get; set; }
        public bool RespectCanCollide{ get; set; }
        public bool BruteForceAllSlow{ get; set; }

        public void AddToFilter(params RObject[] instances)
        {
            
        }
    }
}