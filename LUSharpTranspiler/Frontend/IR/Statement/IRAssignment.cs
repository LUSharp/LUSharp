using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Frontend.IR.Expression;

namespace LUSharpTranspiler.Frontend.IR.Statement
{
    public class IRAssignment : IRStatement
    {
        public IRExpression Left { get; set; }
        public string Operator { get; set; }  // "=", "+=", "-=", etc.
        public IRExpression Right { get; set; }
    }
}
