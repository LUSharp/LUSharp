
using System.Diagnostics.Contracts;
using LUSharpTranspiler.Runtime.STL.Types;

namespace LUSharpTranspiler.Runtime.STL.Classes.Instance
{
    public class HumanoidDescription : Instance
    {
        public string AccessoryBlob { get; set; }
        public string BackAccessory { get; set; }
        public double BodyTypeScale { get; set; }
        public double ClimbAnimation { get; set; }
        public double DepthScale { get; set; }
        public double Face { get; set; }
        public string FaceAccessory { get; set; }
        public double FallAnimation { get; set; }
        public string FrontAccessory { get; set; }
        public double GraphicTShirt { get; set; }
        public string HairAccessory { get; set; }
        public string HatAccessory { get; set; }
        public double Head { get; set; }
        public Color3 HeadColor { get; set; }
        public double HeadScale { get; set; }
        public double HeightScale { get; set; }
        public double IdleAnimation { get; set; }
        public double JumpAnimation { get; set; }
        public double LeftArm { get; set; }
        public Color3 LeftArmColor { get; set; }
        public double LeftLeg { get; set; }
        public Color3 LeftLegColor { get; set; }
        public double MoodAnimation { get; set; }
        public string NeckAccessory { get; set; }
        public double Pants { get; set; }
        public double ProportionScale { get; set; }
        public double RightArm { get; set; }
        public Color3 RightArmColor { get; set; }
        public double RightLeg { get; set; }
        public Color3 RightLegColor { get; set; }
        public double RunAnimation { get; set; }
        public string Shirt { get; set; }
        public string ShouldersAcessory { get; set; }
        public double SwimAnimation { get; set; }
        public double Torso { get; set; }
        public Color3 TorsoColor { get; set; }
        public string WaistAccessory { get; set; }
        public double WalkAnimation { get; set; }
        public double WidthScale { get; set; }

        public void AddEmote(string name, double assetId)
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetAccessories(bool includeRigidAccessories)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, double> GetEmotes()
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetEquippedEmotes()
        {
            throw new NotImplementedException();
        }

        public void RemoveEmote(string name)
        {
            throw new NotImplementedException();
        }

        public void SetAccessories(List<Instance> accessories, bool includeRigidAccessories)
        {
            throw new NotImplementedException();
        }

        public void SetEmotes(Dictionary<string, double> emotes)
        {
            throw new NotImplementedException();
        }

        public void SetEquippedEmotes(List<Instance> emotes)
        {
            throw new NotImplementedException();
        }


        public RBXScriptSignal EmotesChanged(Dictionary<string, double> newEmotes)
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal EquippedEmotesChanged(List<Instance> newAccessories)
        {
            throw new NotImplementedException();
        }
    }
}