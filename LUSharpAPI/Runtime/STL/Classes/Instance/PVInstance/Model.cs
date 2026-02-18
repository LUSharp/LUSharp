using LUSharpAPI.Runtime.STL.Enums;
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance
{
    public class Model : PVInstance
    {
        public ModelStreamingMode ModelStreamingMode { get; set; }
        public BasePart PrimaryPart { get; set; }
        public CFrame WorldPivot{ get; set; }


        public void AddPersistentPlayer(Player playerInstance)
        {
            throw new NotImplementedException();
        }

        public List<object> GetBoundingBox()
        {
            throw new NotImplementedException();
        }

        public Vector3 GetExtentsSize()
        {
            throw new NotImplementedException();
        }

        public List<Instance> GetPersistentPlayers()
        {
            throw new NotImplementedException();
        }

        public double GetScale()
        {
            throw new NotImplementedException();
        }

        public void MoveTo(Vector3 position)
        {
            throw new NotImplementedException();
        }

        public void RemovePersistentPlayer(Player playerInstance)
        {
            throw new NotImplementedException();
        }

        public void ScaleTo(double newScaleFactor)
        {
            throw new NotImplementedException();
        }

        public void TranslateBy(Vector3 delta)
        {
            throw new NotImplementedException();
        }

    }
}
