using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR
{
    internal class IRProgram
    {
        /// <summary>
        /// A list of all modules in this program.
        /// </summary>
        public List<IRModule> Modules { get; } = new List<IRModule>();
    }
}
