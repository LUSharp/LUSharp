using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.AST.SourceConstructor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transpiler.Builder
{
    public class ClassBuilder
    {
        /// <summary>
        /// Name of the class
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Public global class members (Player.Damage(self,plr))
        /// </summary>
        public List<MemberDeclarationSyntax> StaticMembers = new();
        /// <summary>
        /// Members that are accessed by instance. (plr.health)
        /// </summary>
        public List<MemberDeclarationSyntax> InstanceMembers = new();

        public static ClassBuilder FromCClass(ClassDeclarationSyntax classDeclaration)
        {
            ClassBuilder cs = new();

            // The C# class name
            cs.ClassName = classDeclaration.Identifier.ValueText;

            // any Static members that will be accesssed on all instances
            cs.StaticMembers = [.. classDeclaration.Members.Where(x => Helpers.HasModifier(x, SyntaxKind.StaticKeyword))];

            // normal members that will be accessed on instances
            // Only get non static public members.
            cs.InstanceMembers = [.. classDeclaration.Members.Where(x => Helpers.HasModifier(x, SyntaxKind.PublicKeyword) && !Helpers.HasModifier(x, SyntaxKind.StaticKeyword))];

            // For now lets just debug how the class gets parsed
            SourceConstructor.ClassBuilder builder = SourceConstructor.ClassBuilder.Create(cs.ClassName);

            // if we are the main class/function, do our initialization
            if (classDeclaration.Identifier.Text == "Main")
            {
                // we are making a wrapper class that essentially runs our entrypoint
                builder.WithMethod("GameEntry", fn =>
                {

                });

                Logger.Log(builder.Build());
                return cs;
            }

            // If there arent any constructors for the class, then we throw a warning and then say that we are making a default constructor for all public members
            if(!classDeclaration.Members.Any(x => x.IsKind(SyntaxKind.ConstructorDeclaration)))
            {
                Logger.Log(LUSharp.Logger.LogSeverity.Warning, $"No default constructor found for {cs.ClassName}. Creating default.");
                // Build our own constructor
                builder.WithConstructor(c =>
                {
                    // Iterate through each instance member and create a constructor with the responding member name
                    foreach (var p_member in cs.InstanceMembers)
                    {
                        if (p_member.IsKind(SyntaxKind.PropertyDeclaration))
                        {
                            var member = (p_member as PropertyDeclarationSyntax);
                            c.WithParameter(member.Identifier.Text);
                            c.WithPrivateField(member.Identifier.Text, member.Identifier.ValueText);
                        }
                    }
                    
                });
            }
            // create the constructor based on the user defined one
            else
            {
                // reference the constructor code to see how we should make the lua constructor
                var constructor = cs.InstanceMembers.OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
                var parameters = constructor.ParameterList;
                builder.WithConstructor(c =>
                {
                    foreach (var param in parameters.Parameters)
                    {
                        c.WithParameter(param.Identifier.Text);
                    }

                    foreach (var assignment in constructor.Body.Statements.OfType<ExpressionStatementSyntax>())
                    {
                        if (assignment.Expression is AssignmentExpressionSyntax assignExpr)
                        {
                            var left = (assignExpr.Left as MemberAccessExpressionSyntax); // property name
                            var right = (assignExpr.Right as IdentifierNameSyntax); // parameter or expression

                            c.WithPrivateField(left.Name.Identifier.Text, right.Identifier.Text);
                        }
                    }
                });
            }


            // create simple get/setters for instance members
            foreach (var member in cs.InstanceMembers)
            {
                if (member is PropertyDeclarationSyntax prop)
                {
                    // check property accessors for a getter
                    if (prop.AccessorList.Accessors.Any(x => x.IsKind(SyntaxKind.GetAccessorDeclaration)))
                    {
                        builder.WithMethod($"get_{prop.Identifier.Text}", method =>
                        {
                            method.WithParameter("self")
                                .Return($"self.{prop.Identifier.Text}");
                        });
                    }
                    else
                    {
                        Logger.Log(LogSeverity.Warning, $"No get accessor found for {prop.Identifier.Text}, skipping creation of getter method");
                    }

                    if (prop.AccessorList.Accessors.Any(x => x.IsKind(SyntaxKind.SetAccessorDeclaration)))
                    {
                        builder.WithMethod($"set_{prop.Identifier.Text}", method =>
                        {
                            method.WithParameter("self")
                                .WithParameter("value")
                                .Assign($"self.{prop.Identifier.Text}", "value");
                        });
                    }
                    else
                    {
                        Logger.Log(LogSeverity.Warning, $"No set accessor found for {prop.Identifier.Text}, skipping creation of setter method");
                    }
                }
            }
            // Static members for classes
            foreach(var s_member in cs.StaticMembers)
            {
                // static type name {get;set;} = value
                if (s_member is PropertyDeclarationSyntax prop)
                {
                    builder.WithField(prop.Identifier.Text, (prop.Initializer.Value as LiteralExpressionSyntax).Token.Value);
                }
                // static type name = new();
                else if(s_member is FieldDeclarationSyntax prop2)
                {
                    // for now only support simple initializers
                    if(prop2.Declaration.Variables.First().Initializer != null)
                    {
                        // handle different value types here
                        switch (prop2.Declaration.Variables.First().Initializer.Value)
                        {
                            // literal expressions example: static type name = "value";
                            case LiteralExpressionSyntax les:
                                {
                                    builder.WithField(prop2.Declaration.Variables.First().Identifier.Text, les.Token.Value);
                                    break;
                                }
                            // new() expressions example: static type name = new List<string>() {}; can contain data
                            case ImplicitObjectCreationExpressionSyntax:
                                {
                                    // if we have constructor data, add it 
                                    if(prop2.Declaration.Variables.First().Initializer.Value is ImplicitObjectCreationExpressionSyntax ioce && ioce.Initializer != null)
                                    {
                                        var list = new List<object>();
                                        foreach(var expr in ioce.Initializer.Expressions)
                                        {
                                            switch(expr)
                                            {
                                                case LiteralExpressionSyntax lit:
                                                    {
                                                        list.Add(lit.Token.Value);
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        Logger.Log(LogSeverity.Warning, $"Unsupported list initializer expression for {prop2.Declaration.Variables.First().Identifier.Text}, adding null.");
                                                        list.Add(null);
                                                        break;
                                                    }
                                            }
                                        }
                                        builder.WithField(prop2.Declaration.Variables.First().Identifier.Text, list);
                                    }
                                    else
                                        builder.WithField(prop2.Declaration.Variables.First().Identifier.Text, new List<object>());
                                    break;
                                }
                            default:
                                Logger.Log(LogSeverity.Warning, $"Unsupported static field initializer for {prop2.Declaration.Variables.First().Identifier.Text}, skipping.");
                                break;
                        }
                    }
                }
            }

            // Functions/Methods
            foreach(var m in cs.InstanceMembers)
            {
                if(m is MethodDeclarationSyntax method)
                {
                    // implement method, read body and return result
                    //builder.WithMethod(method.Identifier.Text, m =>
                    //{
                        // read the body, make modifications as necessary and return
                        

                    //});

                }
            }


            Logger.Log(builder.Build());

            return cs;
        }


        /// <summary>
        /// Convert the class into the lua code.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            SourceConstructor.ClassBuilder builder = SourceConstructor.ClassBuilder.Create(this.ClassName);

            // iterate each constructor

            return builder.Build();
        }
    }
}
