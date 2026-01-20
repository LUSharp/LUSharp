
using System.Diagnostics.Contracts;
using LUSharpAPI.Runtime.STL.Enums;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class Faces
    {
        public Faces(List<NormalId> normalIds)
        {
            throw new NotImplementedException();
        }

        public bool Top { get; set; }
        public bool Bottom { get; set; }
        public bool Left { get; set; }
        public bool Right { get; set; }
        public bool Back { get; set; }
        public bool Front { get; set; }
    }
}