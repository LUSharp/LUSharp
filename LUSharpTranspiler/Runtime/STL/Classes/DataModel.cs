
using System.Diagnostics.Contracts;
using LUSharpTranspiler.Runtime.STL.Classes.Instance.PVInstance;
using LUSharpTranspiler.Runtime.STL.Enums;
using LUSharpTranspiler.Runtime.STL.Services;
namespace LUSharpTranspiler.Runtime.STL.Classes
{
    public class DataModel
    {   
        public double CreatorId { get; set; }
        public CreatorType CreatorType { get; set; }
        public string Environment{ get; set; }
        public double GameId{ get; set; }
        public string JobId{ get; set; }
        public MatchmakingType MatchmakingType{ get; set; }
        public double PlaceId{ get; set; }
        public double PlaceVersion{ get; set; }
        public string PrivateServerId{ get; set; }
        public double PrivateServerOwnerId{ get; set; }
        public Workspace Workspace{ get; set; }

        public virtual Instance.Instance GetService<T>(string className) where T : Instance.Instance
        {
            throw new NotImplementedException();
        }

        public virtual T GetService<T>() where T : Instance.Instance
        {
            throw new NotImplementedException();
        }

        public virtual T FindService<T>() where T : Instance.Instance
        {
            throw new NotImplementedException();
        }

        public virtual Instance.Instance FindService(string className)
        {
            throw new NotImplementedException();
        }
        public virtual Instance.Instance GetService(string className)
        {
            throw new NotImplementedException();
        }

        public void BindToClose(Action action)
        {
            throw new NotImplementedException();
        }

        public bool IsLoaded()
        {
            throw new NotImplementedException();
        }
    }
}