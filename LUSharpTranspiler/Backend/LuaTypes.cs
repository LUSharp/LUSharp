using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Backend
{
    public class LuaTypes
    {
        public enum LuaPrimitiveType
        {
            Nil,
            Boolean,
            Number,
            String,
        }

        public enum LuaComplexType
        {
            Table,
            Function,
            Enum,
            Tuple,
        }
    }
}
