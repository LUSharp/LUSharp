using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRNew : IRExpression
    {
        public IRType Type { get; set; }
        public List<IRExpression> Args { get; set; }
    }
}
