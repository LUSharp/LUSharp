
using LUSharpAPI.Runtime.STL.Classes;
using LUSharpAPI.Runtime.STL.Enums;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class Content
    {
        public Content fromUri(string uri){
            throw new NotImplementedException();
        }

        public Content fromObject(RObject obj)
        {
            throw new NotImplementedException();
        }

        public Content none { get; set; }
        public ContentSourceType SourceType { get; set; }
        public string Uri{ get; set; }
        public RObject Object { get; set; }
        public RObject Opaque { get; set; }

    }
}
