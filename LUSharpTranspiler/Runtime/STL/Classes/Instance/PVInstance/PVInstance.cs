using LUSharpTranspiler.Runtime.STL.Types;


namespace LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance
{
    public class PVInstance : Instance
    {
        private CFrame OriginP{ get; set; }
        private CFrame PivotOffset{ get; set; }


        public CFrame GetPivot()
        {
            throw new NotImplementedException();
        }

        public void PivotTo(CFrame targetCFrame)
        {
            throw new NotImplementedException();
        }
    }
}