using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Statement;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRBlock : IRStatement
    {
        public List<IRStatement> Statements { get; set; } = new();
    }

}
