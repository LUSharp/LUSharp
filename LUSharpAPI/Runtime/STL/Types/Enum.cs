using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpAPI.Runtime.STL.Types
{
    public class Enum
    {
        public extern List<EnumItem> GetEnumItems();
        public extern Enum? FromName();
        public extern Enum? FromValue();
    }
}
