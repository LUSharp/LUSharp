using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Statement;

namespace LUSharpTranspiler.Frontend.IR
{
    public class IRModule
    {
        public string Name { get; set; }      // e.g. "ReplicatedStorage/Player"
        public List<IRClass> Classes { get; set; } = new();
        public List<IRFunction> Functions { get; set; } = new();  // top-level funcs
        public List<IRStatement> Body { get; set; } = new();      // optional
        public List<IRAttribute> Attributes { get; set; } = new();
    }
}
