
using LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpAPI.Runtime.STL.Enums;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class RaycastResult
    {
        public double Distance{ get; set; }
        public BasePart Instance{ get; set; }
        public Material Material{ get; set; }
        public Vector3 Position{ get; set; }
        public Vector3 Normal{ get; set; }
    }
}