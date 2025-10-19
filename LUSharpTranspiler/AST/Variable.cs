using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LUSharpTranspiler.AST.LuaTypes;

namespace LUSharpTranspiler.AST
{
    public interface IVariable
    {
        /// <summary>
        /// The identifier of the local variable.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The datatype of the variable
        /// </summary>
        public LuaDataType LType { get; }

        public virtual string ToString()
        {
            return Name;
        }
    }
}
