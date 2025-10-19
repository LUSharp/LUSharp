using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LUSharpTranspiler.Transpiler.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LUSharpTranspiler.Transpiler
{
    /// <summary>
    /// A helper class to extract certain information easier from nodes.
    /// </summary>
    internal class Helpers
    {
        /// <summary>
        /// Check if a member has a specific modifier (public/private/internal/static)
        /// </summary>
        /// <param name="member">The member we check</param>
        /// <param name="kind">The modifier we check</param>
        /// <returns></returns>
        internal static bool HasModifier(MemberDeclarationSyntax member, SyntaxKind kind) => member.Modifiers.Any(x => x.IsKind(kind));
    }
}
