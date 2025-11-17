using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IRCall : IRExpression
    {
        public IRExpression Target { get; set; }
        public List<IRExpression> Arguments { get; set; } = new();
    }
}
