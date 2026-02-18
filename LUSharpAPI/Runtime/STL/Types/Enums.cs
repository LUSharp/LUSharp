using LUSharpAPI.Runtime.STL.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class Enums
    {
        public AssetType AssetType;
        public AvatarItemType AvatarItemType;
        public Axis Axis;
        public BulkMoveMode BulkMoveMode;
        public CameraMode CameraMode;
        public CollisionFidelity CollisionFidelity;
        public CreatorType CreatorType;
        public DevCameraOcclusionMode DevCameraOcclusionMode;
        public DevComputerCameraMovementMode DevComputerCameraMovementMode;
        public DevComputerMovementMode DevComputerMovementMode;
        public DevTouchCameraMovementMode DevTouchCameraMovementMode;
        public DevTouchMovementMode DevTouchMovementMode;
        public IKCollisionsMode IKCollisionsMode;
        public JoinSource JoinSource;
        public MatchmakingType MatchmakingType;
        public Material Material;
        public MembershipType MembershipType;
        public ModelLevelOfDetail ModelLevelOfDetail;
        public ModelStreamingMode ModelStreamingMode;
        public NormalId NormalId;
        public PartType PartType;
        public PlayerExitReason PlayerExitReason;
        public RaycastFilterType RaycastFilterType;
        public RenderFidelity RenderFidelity;
        public RotationOrder RotationOrder;
        public SecurityCapability SecurityCapability;
        public SurfaceType SurfaceType;
        public extern List<Enum> GetEnums();
    }
}
