using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.AST
{
    public interface IFunction
    {
        /// <summary>
        /// The name of the function
        /// </summary>
        public string Name { get; set; }

        // Expression list.
    }
}
