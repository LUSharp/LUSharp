using LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpAPI.Runtime.STL.Enums;
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes.Instance
{
    public class Player : Instance
    {
        public double AccountAge { get; set; }
        public bool AutoJumpEnabled { get; set; }

        public double CameraMaxZoomDistanace { get; set; }

        public double CameraMinZoomDistance { get; set; }
        public CameraMode CameraMode { get; set; }

        public bool CanLoadCharacterAppearance { get; set; }
        public Model Character { get; set; }
        public double CharacterAppearanceId { get; set; }
        public DevCameraOcclusionMode DevCameraOcclusionMode { get; set; }
        public DevComputerCameraMovementMode DevComputerCameraMovementMode { get; set; }
        public DevComputerMovementMode DevComputerMovementMode { get; set; }
        public bool DevEnableMouseLock { get; set; }
        public DevTouchCameraMovementMode DevTouchCameraMode { get; set; }
        public DevTouchMovementMode DevTouchMovementMode { get; set; }
        public string DisplayName { get; set; }
        public double FollowUserId { get; set; }
        public double GameplayPaused { get; set; }
        public bool HasVerifiedBadge { get; set; }
        public double HealthDisplayDistance { get; set; }
        public string LocaleId { get; set; }
        public MembershipType MembershipType { get; set; }
        public double NameDisplayDistance { get; set; }
        public bool Neutral { get; set; }
        public string PartyId{ get; set; }
        public Instance ReplicationFocus { get; set; }
        public SpawnLocation RespawnLocation { get; set; }
        public double StepIdOffset { get; set; }
        public Team Team { get; set; }
        public BrickColor TeamColor { get; set; }
        public double UserId { get; set; }

        public void AddReplicationFocus(BasePart part)
        {
            throw new NotImplementedException();
        }

        public void ClearCharacterAppearance()
        {
            throw new NotImplementedException();
        }

        public double DistanceFromCharacter(Vector3 point)
        {
            throw new NotImplementedException();
        }
        
        public List<(double VisitorId, string UserName, string DisplayName, string LastOnline, bool IsOnline, string LastLocation, double PlaceId, string GameId, double LocationType)> GetFriendsOnline(int maxFriends)
        {
            throw new NotImplementedException();
        }
        
        public List<(double SourceGameId, double SourcePlaceId, double ReferredByPlayerId, List<double> Members, object TeleportData, string LaunchData, List<(JoinSource JoinSource, AvatarItemType? itemType, string? AssetId, string? OutfitId, AssetType? AssetType)>)> GetJoinData()
        {
            throw new NotImplementedException();
        }
        
        public Mouse GetMouse()
        {
            throw new NotImplementedException();
        }
        
        public double GetNetworkPing()
        {
            throw new NotImplementedException();
        }

        public double GetRankInGroup(double groupId)
        {
            throw new NotImplementedException();
        }
        

        public string GetRoleInGroup(double groupId)
        {
            throw new NotImplementedException();
        }

        public bool HasAppearanceLoaded()
        {
            throw new NotImplementedException();
        }

        public bool IsFriendsWith(double userId)
        {
            throw new NotImplementedException();
        }

        public bool IsInGroup(double groupId)
        {
            throw new NotImplementedException();
        }

        public bool IsVerified()
        {
            throw new NotImplementedException();
        }

        public void Kick(string message)
        {
            throw new NotImplementedException();
        }

        public void LoadCharacter()
        {
            throw new NotImplementedException();
        }

        public void LoadCharacterWithHumanoidDescription(HumanoidDescription humanoidDescription)
        {
            throw new NotImplementedException();
        }

        public void Move(Vector3 walkDirection, bool relativeToCamera)
        {
            throw new NotImplementedException();
        }

        public void RemoveReplicationFocus(BasePart part)
        {
            throw new NotImplementedException();
        }

        public void RequestStreamAroundAsync(Vector3 position, double timeOut)
        {
            throw new NotImplementedException();
        }












    }
}