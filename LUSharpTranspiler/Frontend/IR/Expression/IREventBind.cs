using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUSharpTranspiler.Frontend.IR.Expression
{
    public class IREventBind : IRExpression
    {
        public IRExpression TargetObject { get; set; }
        public string EventName { get; set; }
        public IRExpression Handler { get; set; }
    }
}
