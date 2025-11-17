using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Statement;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRLambda : IRExpression
    {
        public List<IRParameter> Parameters { get; set; }
        public IRStatement Body { get; set; }
    }
}
