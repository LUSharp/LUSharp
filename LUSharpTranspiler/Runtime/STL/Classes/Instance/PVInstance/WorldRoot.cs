
using System.Diagnostics.Contracts;
using LUSharpTranspiler.Runtime.STL.Enums;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance
{
    public class WorldRoot : PVInstance
    {
        public bool ArePartsTouchingOthers(List<Instance> partList, double overlapIgnored)
        {
            throw new NotImplementedException();
        }
        public RaycastResult? Blockcast(CFrame cframe, Vector3 size, Vector3 direction, RaycastParams rparams)
        {
            throw new NotImplementedException();
        }
        public void BulkMoveTo(List<Instance> partList, List<CFrame> cframeList, BulkMoveMode eventMode)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetPartBoundsInBox(CFrame cframe, Vector3 size, OverlapParams overlapParams)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetPartBoundsInRadius(Vector3 position, double radius, OverlapParams overlapParams)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetPartsInPart(BasePart part, OverlapParams overlapParams)
        {
            throw new NotImplementedException();
        }

        public void IKMoveTo(BasePart part, CFrame target, double translateStiffness, double rotateStiffness, IKCollisionsMode collisionsMode)
        {

        }

        public RaycastResult? Raycast(Vector3 origin, Vector3 direction, RaycastParams rparams)
        {
            throw new NotImplementedException();
        }

        public RaycastResult? Shapecast(BasePart part, Vector3 direction, RaycastParams rparams)
        {
            throw new NotImplementedException();
        }

        public RaycastResult? Spherecast(Vector3 position, double radius, Vector3 direction, RaycastParams rparams)
        {
            throw new NotImplementedException();
        }

        public void StepPhysics(double dt, List<Instance> parts)
        {
            throw new NotImplementedException();
        }
    }
}