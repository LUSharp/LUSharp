using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Transpiler.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Frontend
{
    /// <summary>
    /// Walks through c# code and parses classes and methods.
    /// </summary>
    internal class CodeWalker : CSharpSyntaxWalker
    {
        /// <summary>
        /// Generic C# to lua type mapping
        /// </summary>
        static Dictionary<string, string> s_typeMapping = new()
        {
            { "string", "string"},
            { "int", "number"},
            { "float", "number"},
            { "double", "number"},
            { "bool", "boolean"},
            { "void", "nil"},
            { "object", "table"},
        };

        /// <summary>
        /// Converts a C# class declaration to a Lua table.
        /// </summary>
        /// <param name="node">The class node.</param>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var className = node.Identifier.Text;

            ClassBuilder.FromCClass(node);

            base.VisitClassDeclaration(node);
        }
        /// <summary>
        /// Converts a C# method declaration to a Lua function.
        /// </summary>
        /// <param name="node"></param>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodName = node.Identifier.Text;
            
            // No argument function
            if(node.ParameterList.Parameters.Count == 0)
            {
                base.VisitMethodDeclaration(node);
                return;
            }

            // Function with arguments

            // Convert C# parameters to Lua parameters with type annotations
            var luaArgs = string.Join(", ", node.ParameterList.Parameters.Select(param =>
            {
                var paramName = param.Identifier.Text;
                var paramType = param.Type?.ToString() ?? "var";
                if(s_typeMapping.TryGetValue(paramType, out var luaType))
                {
                    return $"{paramName}: {luaType}";
                }
                else
                {
                    Logger.Log(LogSeverity.Warning, $"No Lua type mapping found for C# type: {paramType}. Defaulting to 'any'.");
                    return $"{paramName}: any"; // default to 'any' if no mapping found
                }
            }));

            base.VisitMethodDeclaration(node);
        }
        
        /// <summary>
        /// Converts a C# expression statement to a Lua statement.
        /// </summary>
        /// <param name="node"></param>
        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var expr = node.Expression;
            
            switch(expr.Kind())
            {
                case SyntaxKind.SimpleAssignmentExpression:
                    {

                        break;
                    }
                // a method call
                case SyntaxKind.InvocationExpression:
                    var invocation = (InvocationExpressionSyntax)expr;
                    string expressionString = invocation.Expression.ToString();

                    // The simplest function call, "function()"
                    if (invocation.Expression.ChildNodes().Any())
                    {
                        var expressionNodes = invocation.Expression.DescendantNodes();
                        // check if the first node is a member access expression
                        if (expressionNodes.First().Kind() == SyntaxKind.IdentifierName)
                        {
                            // special case for Console
                            if (expressionNodes.First().GetText().ToString().Trim() == "Console")
                            {
                                // check the next node for a .WriteLine
                                if (expressionNodes.Skip(1).First().GetText().ToString().Trim() == "WriteLine")
                                    expressionString = "print";
                                else
                                {
                                    Logger.Log(LogSeverity.Warning, "Direct usage of Console is not supported in Lua. Use Console.WriteLine for output.");
                                    break;
                                }
                            }
                        }
                    }
                        
                    // fix argument output for lua typing 

                    // if the argument is a string literal with inline quotes, replace the quotes with ` and close with `
                    if(invocation.ArgumentList.Arguments.Count > 0)
                    {
                        List<string> functionArgs = new();
                        foreach(var arg in invocation.ArgumentList.Arguments)
                        {
                            switch(arg.Expression.Kind())
                            {
                                case SyntaxKind.InterpolatedStringExpression:
                                    {
                                        // replace the string with backticks
                                        var str = arg.Expression.ToString();

                                        // if str starts with $ replace with ""
                                        if (str.StartsWith("$\"") || str.StartsWith("$@\""))
                                            str = str.Replace("$\"", "\"").Replace("$@\"", "\"");

                                        str = str.Replace("\"", "`");

                                        functionArgs.Add(str);

                                        break;
                                    }
                                case SyntaxKind.IdentifierName:
                                    {
                                        functionArgs.Add(arg.Expression.ToString());
                                        break;
                                    }
                                default:
                                    {
                                        Logger.Log(LogSeverity.Warning, $"Unhandled argument kind: {arg.Kind()} for {arg.Expression.ToFullString()}");
                                        break;
                                    }
                            }
                        }
                        //_lua.AppendLineNoIndent($"({string.Join(", ", functionArgs)})");
                    }
                    else
                    {
                        //_lua.AppendLineNoIndent("()");
                    }
                    break;
                // a post-increment expression (e.g., variable++)
                case SyntaxKind.PostIncrementExpression:
                    {
                        var variableName = ((PostfixUnaryExpressionSyntax)expr).Operand.ToString();
                        break;
                    }
                // Handle other expression kinds as needed
                default:
                    {
                        Logger.Log(LogSeverity.Warning, $"Unhandled expression kind: {expr.Kind()}");
                        break;
                    }
            }

        }
        
        /// <summary>
        /// Called when a variable is declared
        /// </summary>
        /// <param name="node"></param>
        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            foreach(var variable in node.Variables)
            {
                var varName = variable.Identifier.Text;
                var varValue = variable.Initializer?.Value.ToString() ?? "nil";


                var initializer = variable.Initializer?.Value;
                // the type of the variable
                
                switch(Type.GetTypeCode(node.Type.GetType()))
                {
                    case TypeCode.Object:
                        {
                            // check if the type is a List or Dictionary
                            var typeStr = node.Type.ToString();

                            switch(typeStr)
                            {
                                case string s when s.StartsWith("List<"):
                                    {
                                        // get the list elements if any
                                        var elements = new List<string>();
                                        if(initializer is ImplicitObjectCreationExpressionSyntax initExpr)
                                        {
                                            foreach(var expr in initExpr.ChildNodes())
                                            {
                                                elements.Add(expr.ToString());
                                            }
                                        }
                                        if(elements.Count > 0)
                                            varValue = $"{string.Join(", ", elements.Last())}"; // Lua table syntax
                                        else
                                            varValue = "{}"; // empty table
                                        break;
                                    }
                                    case string s when s.StartsWith("Dictionary<"):
                                    {
                                        Dictionary<string, string> elements = new();
                                        if(initializer is ImplicitObjectCreationExpressionSyntax initExpr)
                                        {
                                            if(initExpr.ChildNodes().First() is ArgumentListSyntax)
                                            {
                                                if(initExpr.ChildNodes().Last() is InitializerExpressionSyntax)
                                                {
                                                    foreach(var element in initExpr.ChildNodes().Last().ChildNodes())
                                                    {
                                                        if (element is InitializerExpressionSyntax initExprSyntax)
                                                        {
                                                            var key = element.ChildNodes().First();
                                                            var value = element.ChildNodes().Last();
                                                            elements[key.ToString()] = value.ToString();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if(elements.Count > 0)
                                            varValue = "{ " + string.Join(", ", elements.Select(kv => $"[{kv.Key}] = {kv.Value}")) + " }"; // Lua table syntax
                                        else
                                            varValue = "{}";
                                        break;
                                    }
                            }


                            break;
                        }
                    case TypeCode.String:
                        {
                            // if the initializer is not null and is a string literal, replace quotes with backticks
                            if(variable.Initializer?.Value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                            {
                                varValue = varValue.Replace("\"", "`");
                            }
                            break;
                        }
                    case TypeCode.Boolean:
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        {

                            break;
                        }
                    default:
                        {
                            Logger.Log(LogSeverity.Warning, $"Unhandled variable type: {node.Type.ToString()} for variable {varName}. Defaulting to nil.");
                            break;
                        }
                }
            }
            base.VisitVariableDeclaration(node);
        }

        public string GetFinalizedCode()
        {
            return "";
        }
    }
}
