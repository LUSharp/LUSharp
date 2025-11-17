
using LUSharpTranspiler.Runtime.STL.Types;
using LUSharpTranspiler.Runtime.STL.Enums;
using System.ComponentModel;
using LUSharpTranspiler.TestInput.Client;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance
{
    public class BasePart : PVInstance
    {
        public bool Anchored { get; set; }

        public Vector3 AssemblyAngularVelocity { get; set; }
        public Vector3 AssemblyCenterOfMass { get; set; }
        public Vector3 AssemblyLinearVelocity { get; set; }

        public double AssemblyMass { get; set; }
        public BasePart AssemblyRootPart { get; set; }
        public bool AudioCanCollide { get; set; }
        public SurfaceType BackSurface { get; set; }

        public SurfaceType BottomSurface { get; set; }
        public BrickColor BrickColor { get; set; }
        public CFrame CFrame { get; set; }
        public bool CanCollide { get; set; }

        public bool CanQuery { get; set; }
        public bool CanTouch { get; set; }
        public bool CastShadow { get; set; }
        public Vector3 CenterOfMass { get; set; }
        public string CollisionGroup { get; set; }
        public Color3 Color { get; set; }

        public PhysicalProperties CurrentPhysicalProperties { get; set; }
        public PhysicalProperties CustomPhysicalProperties { get; set; }

        public bool EnableFluidForces { get; set; }
        public CFrame ExtentsCFrame { get; set; }
        public Vector3 ExtentsSize { get; set; }

        public SurfaceType FrontSurface { get; set; }
        public SurfaceType LeftSurface { get; set; }
        public double LocalTransparencyModifier { get; set; }
        public bool Locked { get; set; }
        public double Mass { get; set; }

        public bool Massless { get; set; }

        public Material Material { get; set; }
        public string MaterialVariant { get; set; }
        public Vector3 Orientation { get; set; }
        public CFrame PivotOffset { get; set; }
        public Vector3 Position { get; set; }
        public double ReceiveAge { get; set; }
        public double Reflectance { get; set; }
        public double ResizeIncrement { get; set; }
        public Faces ResizeableFaces { get; set; }
        public SurfaceType RightSurface { get; set; }
        public double RootPriority { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Size { get; set; }
        public SurfaceType TopSurface { get; set; }
        public double Transparency { get; set; }



        public Vector3 AngularAccelerationToTorque(Vector3 angAcceleration, Vector3 angVelocity)
        {
            throw new NotImplementedException();
        }

        public void ApplyAngularImpulse(Vector3 impulse)
        {
            throw new NotImplementedException();
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            throw new NotImplementedException();
        }

        public void ApplyImpulseAtPosition(Vector3 impulse, Vector3 position)
        {
            throw new NotImplementedException();
        }

        public bool CanCollideWith(BasePart otherPart)
        {
            throw new NotImplementedException();
        }

        public (bool canSet, string errorReason) CanSetNetworkOwnership()
        {
            throw new NotImplementedException();
        }

        public Vector3 GetClosestPointOnSurface(Vector3 position)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetConnectedParts(bool recursive)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetJoints()
        {
            throw new NotImplementedException();
        }

        public double GetMass()
        {
            throw new NotImplementedException();
        }

        public Instance GetNetworkOwner()
        {
            throw new NotImplementedException();
        }

        public bool GetNetworkOwnershipAuto()
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetNoCollisionConstaints()
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetTouchingParts()
        {
            throw new NotImplementedException();
        }

        public Vector3 GetVelocityAtPosition(Vector3 position)
        {
            throw new NotImplementedException();
        }

        public Instance IntersectAsync(List<Instance> parts, CollisionFidelity collisionFidelity, RenderFidelity renderFidelity)
        {
            throw new NotImplementedException();
        }

        public bool IsGrounded()
        {
            throw new NotImplementedException();
        }

        public bool Resize(NormalId normalId, double deltaAmount)
        {
            throw new NotImplementedException();
        }

        public void SetNetworkOwner(Player playerInstance)
        {
            throw new NotImplementedException();
        }

        public void SetNetworkOwnershipAuto()
        {
            throw new NotImplementedException();
        }

        public Instance SubtractAsync(List<Instance> parts, CollisionFidelity collisionFidelity, RenderFidelity renderFidelity)
        {
            throw new NotImplementedException();
        }

        public Vector3 TorqueToAngularAcceleration(Vector3 torque, Vector3 angVelocity)
        {
            throw new NotImplementedException();
        }

        public Instance UnionAsync(List<Instance> parts, CollisionFidelity collisionFidelity, RenderFidelity renderFidelity)
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal TouchEnded(BasePart part)
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal Touched(BasePart part)
        {
            throw new NotImplementedException();
        }
    }
}
