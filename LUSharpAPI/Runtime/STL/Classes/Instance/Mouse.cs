
using System.Diagnostics;
using System.Diagnostics.Contracts;
using LUSharpAPI.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpAPI.Runtime.STL.Enums;
using LUSharpAPI.Runtime.STL.Types;

namespace LUSharpAPI.Runtime.STL.Classes.Instance
{
    public class Mouse : RObject
    {
        public CFrame Hit { get; set; }
        public string Icon { get; set; }
        public Content IconContent { get; set; }
        public CFrame Origin { get; set; }
        public BasePart Target { get; set; }

        public Instance TargetFilter { get; set; }
        public NormalId TargetSurface { get; set; }
        public Ray UnitRay { get; set; }
        public double ViewSizeX { get; set; }
        public double ViewSizeY { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public RBXScriptSignal Button1Down(){
            throw new NotImplementedException();
        }

        public RBXScriptSignal Button1Up()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal Button2Down()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal Button2Up()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal Idle()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal Move()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal WheelBackward()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal WheelForward()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal WheelLeft()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal WheelRight()
        {
            throw new NotImplementedException();
        }

        public RBXScriptSignal WheelUp()
        {
            throw new NotImplementedException();
        }


    }

    
}