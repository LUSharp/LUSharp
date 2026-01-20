
using LUSharpAPI.Runtime.STL.Classes;
using LUSharpAPI.Runtime.STL.Enums;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class RaycastParams
    {
        public List<RObject> FilterDescendantsInstances{ get; set; }
        public RaycastFilterType FilterType{ get; set; }
        public bool IgnoreWater{ get; set; }
        public string CollisionGroup{ get; set; }
        public bool RespectCanCollide{ get; set; }
        public bool BruteForceAllSlow{ get; set; }

        public void AddToFilter(params RObject[] instances)
        {
            
        }
    }
}