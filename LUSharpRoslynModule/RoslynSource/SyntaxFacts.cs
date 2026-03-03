using System;
using System.Globalization;

namespace Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Defines a set of methods to determine how Unicode characters are treated by the C# compiler.
/// </summary>
public static class SyntaxFacts
{
	// ── Character classification ────────────────────────────────────────

	/// <summary>
	/// Returns true if the Unicode character is a hexadecimal digit.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	/// <returns>true if the character is a hexadecimal digit 0-9, A-F, a-f.</returns>
	internal static bool IsHexDigit(char c)
	{
		if ((c < '0' || c > '9') && (c < 'A' || c > 'F'))
		{
			if (c >= 'a')
			{
				return c <= 'f';
			}
			return false;
		}
		return true;
	}

	/// <summary>
	/// Returns true if the Unicode character is a binary (0-1) digit.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	/// <returns>true if the character is a binary digit.</returns>
	internal static bool IsBinaryDigit(char c)
	{
		return c == '0' || c == '1';
	}

	/// <summary>
	/// Returns true if the Unicode character is a decimal digit.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	/// <returns>true if the Unicode character is a decimal digit.</returns>
	internal static bool IsDecDigit(char c)
	{
		if (c >= '0')
		{
			return c <= '9';
		}
		return false;
	}

	/// <summary>
	/// Returns the value of a hexadecimal Unicode character.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	internal static int HexValue(char c)
	{
		if (c < '0' || c > '9')
		{
			return (c & 0xDF) - 65 + 10;
		}
		return c - 48;
	}

	/// <summary>
	/// Returns the value of a binary Unicode character.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	internal static int BinaryValue(char c)
	{
		return c - 48;
	}

	/// <summary>
	/// Returns the value of a decimal Unicode character.
	/// </summary>
	/// <param name="c">The Unicode character.</param>
	internal static int DecValue(char c)
	{
		return c - 48;
	}

	/// <summary>
	/// Returns true if the Unicode character represents a whitespace.
	/// </summary>
	/// <param name="ch">The Unicode character.</param>
	public static bool IsWhitespace(char ch)
	{
		if (ch != ' ' && ch != '\t' && ch != '\v' && ch != '\f' && ch != '\u00a0' && ch != '\ufeff' && ch != '\u001a')
		{
			if (ch > '\u00ff')
			{
				return CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.SpaceSeparator;
			}
			return false;
		}
		return true;
	}

	/// <summary>
	/// Returns true if the Unicode character is a newline character.
	/// </summary>
	/// <param name="ch">The Unicode character.</param>
	public static bool IsNewLine(char ch)
	{
		if (ch != '\r' && ch != '\n' && ch != '\u0085' && ch != '\u2028')
		{
			return ch == '\u2029';
		}
		return true;
	}

	internal static bool IsNonAsciiQuotationMark(char ch)
	{
		switch (ch)
		{
		case '\u2018':
		case '\u2019':
			return true;
		case '\u201C':
		case '\u201D':
			return true;
		default:
			return false;
		}
	}

	// ── SyntaxKind classification ───────────────────────────────────────

	public static bool IsKeywordKind(SyntaxKind kind)
	{
		if (!IsReservedKeyword(kind))
		{
			return IsContextualKeyword(kind);
		}
		return true;
	}

	public static bool IsReservedKeyword(SyntaxKind kind)
	{
		if ((int)kind >= 8304)
		{
			return (int)kind <= 8384;
		}
		return false;
	}

	public static bool IsAttributeTargetSpecifier(SyntaxKind kind)
	{
		if (kind == SyntaxKind.ReturnKeyword || kind == SyntaxKind.EventKeyword || kind - 8409 <= (SyntaxKind)7)
		{
			return true;
		}
		return false;
	}

	public static bool IsAccessibilityModifier(SyntaxKind kind)
	{
		if (kind - 8343 <= (SyntaxKind)3)
		{
			return true;
		}
		return false;
	}

	public static bool IsPreprocessorKeyword(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.TrueKeyword:
		case SyntaxKind.FalseKeyword:
		case SyntaxKind.IfKeyword:
		case SyntaxKind.ElseKeyword:
		case SyntaxKind.DefaultKeyword:
		case SyntaxKind.ElifKeyword:
		case SyntaxKind.EndIfKeyword:
		case SyntaxKind.RegionKeyword:
		case SyntaxKind.EndRegionKeyword:
		case SyntaxKind.DefineKeyword:
		case SyntaxKind.UndefKeyword:
		case SyntaxKind.WarningKeyword:
		case SyntaxKind.ErrorKeyword:
		case SyntaxKind.LineKeyword:
		case SyntaxKind.PragmaKeyword:
		case SyntaxKind.HiddenKeyword:
		case SyntaxKind.ChecksumKeyword:
		case SyntaxKind.DisableKeyword:
		case SyntaxKind.RestoreKeyword:
		case SyntaxKind.ReferenceKeyword:
		case SyntaxKind.LoadKeyword:
		case SyntaxKind.NullableKeyword:
		case SyntaxKind.EnableKeyword:
		case SyntaxKind.WarningsKeyword:
		case SyntaxKind.AnnotationsKeyword:
			return true;
		default:
			return false;
		}
	}

	/// <summary>
	/// Some preprocessor keywords are only keywords when they appear after a
	/// hash sign (#).  For these keywords, the lexer will produce tokens with
	/// Kind = SyntaxKind.IdentifierToken and ContextualKind set to the keyword
	/// SyntaxKind.
	/// </summary>
	internal static bool IsPreprocessorContextualKeyword(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.TrueKeyword:
		case SyntaxKind.FalseKeyword:
		case SyntaxKind.DefaultKeyword:
		case SyntaxKind.HiddenKeyword:
		case SyntaxKind.ChecksumKeyword:
		case SyntaxKind.DisableKeyword:
		case SyntaxKind.RestoreKeyword:
		case SyntaxKind.EnableKeyword:
		case SyntaxKind.WarningsKeyword:
		case SyntaxKind.AnnotationsKeyword:
			return false;
		default:
			return IsPreprocessorKeyword(kind);
		}
	}

	public static bool IsPunctuation(SyntaxKind kind)
	{
		if ((int)kind >= 8193)
		{
			return (int)kind <= 8287;
		}
		return false;
	}

	public static bool IsLanguagePunctuation(SyntaxKind kind)
	{
		if (IsPunctuation(kind) && !IsPreprocessorKeyword(kind))
		{
			return !IsDebuggerSpecialPunctuation(kind);
		}
		return false;
	}

	public static bool IsPreprocessorPunctuation(SyntaxKind kind)
	{
		return kind == SyntaxKind.HashToken;
	}

	private static bool IsDebuggerSpecialPunctuation(SyntaxKind kind)
	{
		return kind == SyntaxKind.DollarToken;
	}

	public static bool IsPunctuationOrKeyword(SyntaxKind kind)
	{
		if ((int)kind >= 8193)
		{
			return (int)kind <= 8496;
		}
		return false;
	}

	internal static bool IsLiteral(SyntaxKind kind)
	{
		if (kind - 8508 <= (SyntaxKind)6 || kind - 8518 <= (SyntaxKind)4)
		{
			return true;
		}
		return false;
	}

	public static bool IsAnyToken(SyntaxKind kind)
	{
		if ((int)kind >= 8193 && (int)kind < 8539)
		{
			return true;
		}
		switch (kind)
		{
		case SyntaxKind.InterpolatedStringStartToken:
		case SyntaxKind.InterpolatedStringEndToken:
		case SyntaxKind.InterpolatedVerbatimStringStartToken:
		case SyntaxKind.LoadKeyword:
		case SyntaxKind.NullableKeyword:
		case SyntaxKind.EnableKeyword:
		case SyntaxKind.UnderscoreToken:
		case SyntaxKind.InterpolatedStringToken:
		case SyntaxKind.InterpolatedStringTextToken:
		case SyntaxKind.SingleLineRawStringLiteralToken:
		case SyntaxKind.MultiLineRawStringLiteralToken:
		case SyntaxKind.InterpolatedSingleLineRawStringStartToken:
		case SyntaxKind.InterpolatedMultiLineRawStringStartToken:
		case SyntaxKind.InterpolatedRawStringEndToken:
			return true;
		default:
			return false;
		}
	}

	public static bool IsTrivia(SyntaxKind kind)
	{
		if (kind - 8539 <= (SyntaxKind)7 || kind == SyntaxKind.ConflictMarkerTrivia)
		{
			return true;
		}
		return IsPreprocessorDirective(kind);
	}

	public static bool IsPreprocessorDirective(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.IfDirectiveTrivia:
		case SyntaxKind.ElifDirectiveTrivia:
		case SyntaxKind.ElseDirectiveTrivia:
		case SyntaxKind.EndIfDirectiveTrivia:
		case SyntaxKind.RegionDirectiveTrivia:
		case SyntaxKind.EndRegionDirectiveTrivia:
		case SyntaxKind.DefineDirectiveTrivia:
		case SyntaxKind.UndefDirectiveTrivia:
		case SyntaxKind.ErrorDirectiveTrivia:
		case SyntaxKind.WarningDirectiveTrivia:
		case SyntaxKind.LineDirectiveTrivia:
		case SyntaxKind.PragmaWarningDirectiveTrivia:
		case SyntaxKind.PragmaChecksumDirectiveTrivia:
		case SyntaxKind.ReferenceDirectiveTrivia:
		case SyntaxKind.BadDirectiveTrivia:
		case SyntaxKind.ShebangDirectiveTrivia:
		case SyntaxKind.LoadDirectiveTrivia:
		case SyntaxKind.NullableDirectiveTrivia:
		case SyntaxKind.LineSpanDirectiveTrivia:
		case SyntaxKind.IgnoredDirectiveTrivia:
			return true;
		default:
			return false;
		}
	}

	public static bool IsName(SyntaxKind kind)
	{
		if (kind - 8616 <= (SyntaxKind)2 || kind == SyntaxKind.AliasQualifiedName)
		{
			return true;
		}
		return false;
	}

	public static bool IsPredefinedType(SyntaxKind kind)
	{
		if (kind - 8304 <= (SyntaxKind)15)
		{
			return true;
		}
		return false;
	}

	public static bool IsTypeSyntax(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.PredefinedType:
		case SyntaxKind.ArrayType:
		case SyntaxKind.PointerType:
		case SyntaxKind.NullableType:
		case SyntaxKind.TupleType:
		case SyntaxKind.FunctionPointerType:
			return true;
		default:
			return IsName(kind);
		}
	}

	public static bool IsTypeDeclaration(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.ClassDeclaration:
		case SyntaxKind.StructDeclaration:
		case SyntaxKind.InterfaceDeclaration:
		case SyntaxKind.EnumDeclaration:
		case SyntaxKind.DelegateDeclaration:
		case SyntaxKind.RecordDeclaration:
		case SyntaxKind.RecordStructDeclaration:
		case SyntaxKind.ExtensionBlockDeclaration:
			return true;
		default:
			return false;
		}
	}

	public static bool IsNamespaceMemberDeclaration(SyntaxKind kind)
	{
		if (!IsTypeDeclaration(kind) && kind != SyntaxKind.NamespaceDeclaration)
		{
			return kind == SyntaxKind.FileScopedNamespaceDeclaration;
		}
		return true;
	}

	/// <summary>
	/// Member declarations that can appear in global code (other than type declarations).
	/// </summary>
	public static bool IsGlobalMemberDeclaration(SyntaxKind kind)
	{
		if (kind == SyntaxKind.GlobalStatement || kind - 8873 <= (SyntaxKind)2 || kind - 8892 <= SyntaxKind.List)
		{
			return true;
		}
		return false;
	}

	// ── Operator classification ─────────────────────────────────────────

	public static bool IsAnyUnaryExpression(SyntaxKind token)
	{
		if (!IsPrefixUnaryExpression(token))
		{
			return IsPostfixUnaryExpression(token);
		}
		return true;
	}

	public static bool IsPrefixUnaryExpression(SyntaxKind token)
	{
		return GetPrefixUnaryExpression(token) != SyntaxKind.None;
	}

	public static bool IsPrefixUnaryExpressionOperatorToken(SyntaxKind token)
	{
		return GetPrefixUnaryExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetPrefixUnaryExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.PlusToken => SyntaxKind.UnaryPlusExpression,
			SyntaxKind.MinusToken => SyntaxKind.UnaryMinusExpression,
			SyntaxKind.TildeToken => SyntaxKind.BitwiseNotExpression,
			SyntaxKind.ExclamationToken => SyntaxKind.LogicalNotExpression,
			SyntaxKind.PlusPlusToken => SyntaxKind.PreIncrementExpression,
			SyntaxKind.MinusMinusToken => SyntaxKind.PreDecrementExpression,
			SyntaxKind.AmpersandToken => SyntaxKind.AddressOfExpression,
			SyntaxKind.AsteriskToken => SyntaxKind.PointerIndirectionExpression,
			SyntaxKind.CaretToken => SyntaxKind.IndexExpression,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsPostfixUnaryExpression(SyntaxKind token)
	{
		return GetPostfixUnaryExpression(token) != SyntaxKind.None;
	}

	public static bool IsPostfixUnaryExpressionToken(SyntaxKind token)
	{
		return GetPostfixUnaryExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetPostfixUnaryExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.PlusPlusToken => SyntaxKind.PostIncrementExpression,
			SyntaxKind.MinusMinusToken => SyntaxKind.PostDecrementExpression,
			SyntaxKind.ExclamationToken => SyntaxKind.SuppressNullableWarningExpression,
			_ => SyntaxKind.None,
		};
	}

	internal static bool IsIncrementOrDecrementOperator(SyntaxKind token)
	{
		if (token - 8262 <= SyntaxKind.List)
		{
			return true;
		}
		return false;
	}

	public static bool IsUnaryOperatorDeclarationToken(SyntaxKind token)
	{
		if (!IsPrefixUnaryExpressionOperatorToken(token) && token != SyntaxKind.TrueKeyword)
		{
			return token == SyntaxKind.FalseKeyword;
		}
		return true;
	}

	public static bool IsAnyOverloadableOperator(SyntaxKind kind)
	{
		if (!IsOverloadableBinaryOperator(kind) && !IsOverloadableUnaryOperator(kind))
		{
			return IsOverloadableCompoundAssignmentOperator(kind);
		}
		return true;
	}

	public static bool IsOverloadableBinaryOperator(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.PercentToken:
		case SyntaxKind.CaretToken:
		case SyntaxKind.AmpersandToken:
		case SyntaxKind.AsteriskToken:
		case SyntaxKind.MinusToken:
		case SyntaxKind.PlusToken:
		case SyntaxKind.BarToken:
		case SyntaxKind.LessThanToken:
		case SyntaxKind.GreaterThanToken:
		case SyntaxKind.SlashToken:
		case SyntaxKind.ExclamationEqualsToken:
		case SyntaxKind.EqualsEqualsToken:
		case SyntaxKind.LessThanEqualsToken:
		case SyntaxKind.LessThanLessThanToken:
		case SyntaxKind.GreaterThanEqualsToken:
		case SyntaxKind.GreaterThanGreaterThanToken:
		case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
			return true;
		default:
			return false;
		}
	}

	public static bool IsOverloadableUnaryOperator(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.TildeToken:
		case SyntaxKind.ExclamationToken:
		case SyntaxKind.MinusToken:
		case SyntaxKind.PlusToken:
		case SyntaxKind.MinusMinusToken:
		case SyntaxKind.PlusPlusToken:
		case SyntaxKind.TrueKeyword:
		case SyntaxKind.FalseKeyword:
			return true;
		default:
			return false;
		}
	}

	public static bool IsOverloadableCompoundAssignmentOperator(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.LessThanLessThanEqualsToken:
		case SyntaxKind.GreaterThanGreaterThanEqualsToken:
		case SyntaxKind.SlashEqualsToken:
		case SyntaxKind.AsteriskEqualsToken:
		case SyntaxKind.BarEqualsToken:
		case SyntaxKind.AmpersandEqualsToken:
		case SyntaxKind.PlusEqualsToken:
		case SyntaxKind.MinusEqualsToken:
		case SyntaxKind.CaretEqualsToken:
		case SyntaxKind.PercentEqualsToken:
		case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
			return true;
		default:
			return false;
		}
	}

	// ── Expression/statement mapping ────────────────────────────────────

	public static bool IsPrimaryFunction(SyntaxKind keyword)
	{
		return GetPrimaryFunction(keyword) != SyntaxKind.None;
	}

	public static SyntaxKind GetPrimaryFunction(SyntaxKind keyword)
	{
		return keyword switch
		{
			SyntaxKind.MakeRefKeyword => SyntaxKind.MakeRefExpression,
			SyntaxKind.RefTypeKeyword => SyntaxKind.RefTypeExpression,
			SyntaxKind.RefValueKeyword => SyntaxKind.RefValueExpression,
			SyntaxKind.CheckedKeyword => SyntaxKind.CheckedExpression,
			SyntaxKind.UncheckedKeyword => SyntaxKind.UncheckedExpression,
			SyntaxKind.DefaultKeyword => SyntaxKind.DefaultExpression,
			SyntaxKind.TypeOfKeyword => SyntaxKind.TypeOfExpression,
			SyntaxKind.SizeOfKeyword => SyntaxKind.SizeOfExpression,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsLiteralExpression(SyntaxKind token)
	{
		return GetLiteralExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetLiteralExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.StringLiteralToken => SyntaxKind.StringLiteralExpression,
			SyntaxKind.Utf8StringLiteralToken => SyntaxKind.Utf8StringLiteralExpression,
			SyntaxKind.SingleLineRawStringLiteralToken => SyntaxKind.StringLiteralExpression,
			SyntaxKind.Utf8SingleLineRawStringLiteralToken => SyntaxKind.Utf8StringLiteralExpression,
			SyntaxKind.MultiLineRawStringLiteralToken => SyntaxKind.StringLiteralExpression,
			SyntaxKind.Utf8MultiLineRawStringLiteralToken => SyntaxKind.Utf8StringLiteralExpression,
			SyntaxKind.CharacterLiteralToken => SyntaxKind.CharacterLiteralExpression,
			SyntaxKind.NumericLiteralToken => SyntaxKind.NumericLiteralExpression,
			SyntaxKind.NullKeyword => SyntaxKind.NullLiteralExpression,
			SyntaxKind.TrueKeyword => SyntaxKind.TrueLiteralExpression,
			SyntaxKind.FalseKeyword => SyntaxKind.FalseLiteralExpression,
			SyntaxKind.ArgListKeyword => SyntaxKind.ArgListExpression,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsInstanceExpression(SyntaxKind token)
	{
		return GetInstanceExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetInstanceExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.ThisKeyword => SyntaxKind.ThisExpression,
			SyntaxKind.BaseKeyword => SyntaxKind.BaseExpression,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsBinaryExpression(SyntaxKind token)
	{
		return GetBinaryExpression(token) != SyntaxKind.None;
	}

	public static bool IsBinaryExpressionOperatorToken(SyntaxKind token)
	{
		return GetBinaryExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetBinaryExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.QuestionQuestionToken => SyntaxKind.CoalesceExpression,
			SyntaxKind.IsKeyword => SyntaxKind.IsExpression,
			SyntaxKind.AsKeyword => SyntaxKind.AsExpression,
			SyntaxKind.BarToken => SyntaxKind.BitwiseOrExpression,
			SyntaxKind.CaretToken => SyntaxKind.ExclusiveOrExpression,
			SyntaxKind.AmpersandToken => SyntaxKind.BitwiseAndExpression,
			SyntaxKind.EqualsEqualsToken => SyntaxKind.EqualsExpression,
			SyntaxKind.ExclamationEqualsToken => SyntaxKind.NotEqualsExpression,
			SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
			SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
			SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.LessThanLessThanToken => SyntaxKind.LeftShiftExpression,
			SyntaxKind.GreaterThanGreaterThanToken => SyntaxKind.RightShiftExpression,
			SyntaxKind.GreaterThanGreaterThanGreaterThanToken => SyntaxKind.UnsignedRightShiftExpression,
			SyntaxKind.PlusToken => SyntaxKind.AddExpression,
			SyntaxKind.MinusToken => SyntaxKind.SubtractExpression,
			SyntaxKind.AsteriskToken => SyntaxKind.MultiplyExpression,
			SyntaxKind.SlashToken => SyntaxKind.DivideExpression,
			SyntaxKind.PercentToken => SyntaxKind.ModuloExpression,
			SyntaxKind.AmpersandAmpersandToken => SyntaxKind.LogicalAndExpression,
			SyntaxKind.BarBarToken => SyntaxKind.LogicalOrExpression,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsAssignmentExpression(SyntaxKind kind)
	{
		if (kind - 8714 <= (SyntaxKind)12)
		{
			return true;
		}
		return false;
	}

	public static bool IsAssignmentExpressionOperatorToken(SyntaxKind token)
	{
		switch (token)
		{
		case SyntaxKind.EqualsToken:
		case SyntaxKind.LessThanLessThanEqualsToken:
		case SyntaxKind.GreaterThanGreaterThanEqualsToken:
		case SyntaxKind.SlashEqualsToken:
		case SyntaxKind.AsteriskEqualsToken:
		case SyntaxKind.BarEqualsToken:
		case SyntaxKind.AmpersandEqualsToken:
		case SyntaxKind.PlusEqualsToken:
		case SyntaxKind.MinusEqualsToken:
		case SyntaxKind.CaretEqualsToken:
		case SyntaxKind.PercentEqualsToken:
		case SyntaxKind.QuestionQuestionEqualsToken:
		case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
			return true;
		default:
			return false;
		}
	}

	public static SyntaxKind GetAssignmentExpression(SyntaxKind token)
	{
		return token switch
		{
			SyntaxKind.BarEqualsToken => SyntaxKind.OrAssignmentExpression,
			SyntaxKind.AmpersandEqualsToken => SyntaxKind.AndAssignmentExpression,
			SyntaxKind.CaretEqualsToken => SyntaxKind.ExclusiveOrAssignmentExpression,
			SyntaxKind.LessThanLessThanEqualsToken => SyntaxKind.LeftShiftAssignmentExpression,
			SyntaxKind.GreaterThanGreaterThanEqualsToken => SyntaxKind.RightShiftAssignmentExpression,
			SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken => SyntaxKind.UnsignedRightShiftAssignmentExpression,
			SyntaxKind.PlusEqualsToken => SyntaxKind.AddAssignmentExpression,
			SyntaxKind.MinusEqualsToken => SyntaxKind.SubtractAssignmentExpression,
			SyntaxKind.AsteriskEqualsToken => SyntaxKind.MultiplyAssignmentExpression,
			SyntaxKind.SlashEqualsToken => SyntaxKind.DivideAssignmentExpression,
			SyntaxKind.PercentEqualsToken => SyntaxKind.ModuloAssignmentExpression,
			SyntaxKind.EqualsToken => SyntaxKind.SimpleAssignmentExpression,
			SyntaxKind.QuestionQuestionEqualsToken => SyntaxKind.CoalesceAssignmentExpression,
			_ => SyntaxKind.None,
		};
	}

	public static SyntaxKind GetCheckStatement(SyntaxKind keyword)
	{
		return keyword switch
		{
			SyntaxKind.CheckedKeyword => SyntaxKind.CheckedStatement,
			SyntaxKind.UncheckedKeyword => SyntaxKind.UncheckedStatement,
			_ => SyntaxKind.None,
		};
	}

	public static SyntaxKind GetAccessorDeclarationKind(SyntaxKind keyword)
	{
		return keyword switch
		{
			SyntaxKind.GetKeyword => SyntaxKind.GetAccessorDeclaration,
			SyntaxKind.SetKeyword => SyntaxKind.SetAccessorDeclaration,
			SyntaxKind.InitKeyword => SyntaxKind.InitAccessorDeclaration,
			SyntaxKind.AddKeyword => SyntaxKind.AddAccessorDeclaration,
			SyntaxKind.RemoveKeyword => SyntaxKind.RemoveAccessorDeclaration,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsAccessorDeclaration(SyntaxKind kind)
	{
		if (kind - 8896 <= (SyntaxKind)3 || kind == SyntaxKind.InitAccessorDeclaration)
		{
			return true;
		}
		return false;
	}

	public static bool IsAccessorDeclarationKeyword(SyntaxKind keyword)
	{
		if (keyword - 8417 <= (SyntaxKind)3 || keyword == SyntaxKind.InitKeyword)
		{
			return true;
		}
		return false;
	}

	public static SyntaxKind GetSwitchLabelKind(SyntaxKind keyword)
	{
		return keyword switch
		{
			SyntaxKind.CaseKeyword => SyntaxKind.CaseSwitchLabel,
			SyntaxKind.DefaultKeyword => SyntaxKind.DefaultSwitchLabel,
			_ => SyntaxKind.None,
		};
	}

	public static SyntaxKind GetBaseTypeDeclarationKind(SyntaxKind kind)
	{
		if (kind != SyntaxKind.EnumKeyword)
		{
			return GetTypeDeclarationKind(kind);
		}
		return SyntaxKind.EnumDeclaration;
	}

	public static SyntaxKind GetTypeDeclarationKind(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.ClassKeyword => SyntaxKind.ClassDeclaration,
			SyntaxKind.StructKeyword => SyntaxKind.StructDeclaration,
			SyntaxKind.InterfaceKeyword => SyntaxKind.InterfaceDeclaration,
			SyntaxKind.RecordKeyword => SyntaxKind.RecordDeclaration,
			_ => SyntaxKind.None,
		};
	}

	// ── Keyword/text lookup ─────────────────────────────────────────────

	public static SyntaxKind GetKeywordKind(string text)
	{
		return text switch
		{
			"bool" => SyntaxKind.BoolKeyword,
			"byte" => SyntaxKind.ByteKeyword,
			"sbyte" => SyntaxKind.SByteKeyword,
			"short" => SyntaxKind.ShortKeyword,
			"ushort" => SyntaxKind.UShortKeyword,
			"int" => SyntaxKind.IntKeyword,
			"uint" => SyntaxKind.UIntKeyword,
			"long" => SyntaxKind.LongKeyword,
			"ulong" => SyntaxKind.ULongKeyword,
			"double" => SyntaxKind.DoubleKeyword,
			"float" => SyntaxKind.FloatKeyword,
			"decimal" => SyntaxKind.DecimalKeyword,
			"string" => SyntaxKind.StringKeyword,
			"char" => SyntaxKind.CharKeyword,
			"void" => SyntaxKind.VoidKeyword,
			"object" => SyntaxKind.ObjectKeyword,
			"typeof" => SyntaxKind.TypeOfKeyword,
			"sizeof" => SyntaxKind.SizeOfKeyword,
			"null" => SyntaxKind.NullKeyword,
			"true" => SyntaxKind.TrueKeyword,
			"false" => SyntaxKind.FalseKeyword,
			"if" => SyntaxKind.IfKeyword,
			"else" => SyntaxKind.ElseKeyword,
			"while" => SyntaxKind.WhileKeyword,
			"for" => SyntaxKind.ForKeyword,
			"foreach" => SyntaxKind.ForEachKeyword,
			"do" => SyntaxKind.DoKeyword,
			"switch" => SyntaxKind.SwitchKeyword,
			"case" => SyntaxKind.CaseKeyword,
			"default" => SyntaxKind.DefaultKeyword,
			"lock" => SyntaxKind.LockKeyword,
			"try" => SyntaxKind.TryKeyword,
			"throw" => SyntaxKind.ThrowKeyword,
			"catch" => SyntaxKind.CatchKeyword,
			"finally" => SyntaxKind.FinallyKeyword,
			"goto" => SyntaxKind.GotoKeyword,
			"break" => SyntaxKind.BreakKeyword,
			"continue" => SyntaxKind.ContinueKeyword,
			"return" => SyntaxKind.ReturnKeyword,
			"public" => SyntaxKind.PublicKeyword,
			"private" => SyntaxKind.PrivateKeyword,
			"internal" => SyntaxKind.InternalKeyword,
			"protected" => SyntaxKind.ProtectedKeyword,
			"static" => SyntaxKind.StaticKeyword,
			"readonly" => SyntaxKind.ReadOnlyKeyword,
			"sealed" => SyntaxKind.SealedKeyword,
			"const" => SyntaxKind.ConstKeyword,
			"fixed" => SyntaxKind.FixedKeyword,
			"stackalloc" => SyntaxKind.StackAllocKeyword,
			"volatile" => SyntaxKind.VolatileKeyword,
			"new" => SyntaxKind.NewKeyword,
			"override" => SyntaxKind.OverrideKeyword,
			"abstract" => SyntaxKind.AbstractKeyword,
			"virtual" => SyntaxKind.VirtualKeyword,
			"event" => SyntaxKind.EventKeyword,
			"extern" => SyntaxKind.ExternKeyword,
			"ref" => SyntaxKind.RefKeyword,
			"out" => SyntaxKind.OutKeyword,
			"in" => SyntaxKind.InKeyword,
			"is" => SyntaxKind.IsKeyword,
			"as" => SyntaxKind.AsKeyword,
			"params" => SyntaxKind.ParamsKeyword,
			"__arglist" => SyntaxKind.ArgListKeyword,
			"__makeref" => SyntaxKind.MakeRefKeyword,
			"__reftype" => SyntaxKind.RefTypeKeyword,
			"__refvalue" => SyntaxKind.RefValueKeyword,
			"this" => SyntaxKind.ThisKeyword,
			"base" => SyntaxKind.BaseKeyword,
			"namespace" => SyntaxKind.NamespaceKeyword,
			"using" => SyntaxKind.UsingKeyword,
			"class" => SyntaxKind.ClassKeyword,
			"struct" => SyntaxKind.StructKeyword,
			"interface" => SyntaxKind.InterfaceKeyword,
			"enum" => SyntaxKind.EnumKeyword,
			"delegate" => SyntaxKind.DelegateKeyword,
			"checked" => SyntaxKind.CheckedKeyword,
			"unchecked" => SyntaxKind.UncheckedKeyword,
			"unsafe" => SyntaxKind.UnsafeKeyword,
			"operator" => SyntaxKind.OperatorKeyword,
			"implicit" => SyntaxKind.ImplicitKeyword,
			"explicit" => SyntaxKind.ExplicitKeyword,
			_ => SyntaxKind.None,
		};
	}

	public static SyntaxKind GetPreprocessorKeywordKind(string text)
	{
		return text switch
		{
			"true" => SyntaxKind.TrueKeyword,
			"false" => SyntaxKind.FalseKeyword,
			"default" => SyntaxKind.DefaultKeyword,
			"if" => SyntaxKind.IfKeyword,
			"else" => SyntaxKind.ElseKeyword,
			"elif" => SyntaxKind.ElifKeyword,
			"endif" => SyntaxKind.EndIfKeyword,
			"region" => SyntaxKind.RegionKeyword,
			"endregion" => SyntaxKind.EndRegionKeyword,
			"define" => SyntaxKind.DefineKeyword,
			"undef" => SyntaxKind.UndefKeyword,
			"warning" => SyntaxKind.WarningKeyword,
			"error" => SyntaxKind.ErrorKeyword,
			"line" => SyntaxKind.LineKeyword,
			"pragma" => SyntaxKind.PragmaKeyword,
			"hidden" => SyntaxKind.HiddenKeyword,
			"checksum" => SyntaxKind.ChecksumKeyword,
			"disable" => SyntaxKind.DisableKeyword,
			"restore" => SyntaxKind.RestoreKeyword,
			"r" => SyntaxKind.ReferenceKeyword,
			"load" => SyntaxKind.LoadKeyword,
			"nullable" => SyntaxKind.NullableKeyword,
			"enable" => SyntaxKind.EnableKeyword,
			"warnings" => SyntaxKind.WarningsKeyword,
			"annotations" => SyntaxKind.AnnotationsKeyword,
			_ => SyntaxKind.None,
		};
	}

	public static bool IsContextualKeyword(SyntaxKind kind)
	{
		switch (kind)
		{
		case SyntaxKind.YieldKeyword:
		case SyntaxKind.PartialKeyword:
		case SyntaxKind.AliasKeyword:
		case SyntaxKind.GlobalKeyword:
		case SyntaxKind.AssemblyKeyword:
		case SyntaxKind.ModuleKeyword:
		case SyntaxKind.TypeKeyword:
		case SyntaxKind.FieldKeyword:
		case SyntaxKind.MethodKeyword:
		case SyntaxKind.ParamKeyword:
		case SyntaxKind.PropertyKeyword:
		case SyntaxKind.TypeVarKeyword:
		case SyntaxKind.GetKeyword:
		case SyntaxKind.SetKeyword:
		case SyntaxKind.AddKeyword:
		case SyntaxKind.RemoveKeyword:
		case SyntaxKind.WhereKeyword:
		case SyntaxKind.FromKeyword:
		case SyntaxKind.GroupKeyword:
		case SyntaxKind.JoinKeyword:
		case SyntaxKind.IntoKeyword:
		case SyntaxKind.LetKeyword:
		case SyntaxKind.ByKeyword:
		case SyntaxKind.SelectKeyword:
		case SyntaxKind.OrderByKeyword:
		case SyntaxKind.OnKeyword:
		case SyntaxKind.EqualsKeyword:
		case SyntaxKind.AscendingKeyword:
		case SyntaxKind.DescendingKeyword:
		case SyntaxKind.NameOfKeyword:
		case SyntaxKind.AsyncKeyword:
		case SyntaxKind.AwaitKeyword:
		case SyntaxKind.WhenKeyword:
		case SyntaxKind.OrKeyword:
		case SyntaxKind.AndKeyword:
		case SyntaxKind.NotKeyword:
		case SyntaxKind.WithKeyword:
		case SyntaxKind.InitKeyword:
		case SyntaxKind.RecordKeyword:
		case SyntaxKind.ManagedKeyword:
		case SyntaxKind.UnmanagedKeyword:
		case SyntaxKind.RequiredKeyword:
		case SyntaxKind.ScopedKeyword:
		case SyntaxKind.FileKeyword:
		case SyntaxKind.AllowsKeyword:
		case SyntaxKind.ExtensionKeyword:
		case SyntaxKind.VarKeyword:
		case SyntaxKind.UnderscoreToken:
			return true;
		default:
			return false;
		}
	}

	public static bool IsQueryContextualKeyword(SyntaxKind kind)
	{
		if (kind - 8421 <= (SyntaxKind)12)
		{
			return true;
		}
		return false;
	}

	public static SyntaxKind GetContextualKeywordKind(string text)
	{
		return text switch
		{
			"yield" => SyntaxKind.YieldKeyword,
			"partial" => SyntaxKind.PartialKeyword,
			"from" => SyntaxKind.FromKeyword,
			"group" => SyntaxKind.GroupKeyword,
			"join" => SyntaxKind.JoinKeyword,
			"into" => SyntaxKind.IntoKeyword,
			"let" => SyntaxKind.LetKeyword,
			"by" => SyntaxKind.ByKeyword,
			"where" => SyntaxKind.WhereKeyword,
			"select" => SyntaxKind.SelectKeyword,
			"get" => SyntaxKind.GetKeyword,
			"set" => SyntaxKind.SetKeyword,
			"add" => SyntaxKind.AddKeyword,
			"remove" => SyntaxKind.RemoveKeyword,
			"orderby" => SyntaxKind.OrderByKeyword,
			"alias" => SyntaxKind.AliasKeyword,
			"on" => SyntaxKind.OnKeyword,
			"equals" => SyntaxKind.EqualsKeyword,
			"ascending" => SyntaxKind.AscendingKeyword,
			"descending" => SyntaxKind.DescendingKeyword,
			"assembly" => SyntaxKind.AssemblyKeyword,
			"module" => SyntaxKind.ModuleKeyword,
			"type" => SyntaxKind.TypeKeyword,
			"field" => SyntaxKind.FieldKeyword,
			"method" => SyntaxKind.MethodKeyword,
			"param" => SyntaxKind.ParamKeyword,
			"property" => SyntaxKind.PropertyKeyword,
			"typevar" => SyntaxKind.TypeVarKeyword,
			"global" => SyntaxKind.GlobalKeyword,
			"async" => SyntaxKind.AsyncKeyword,
			"await" => SyntaxKind.AwaitKeyword,
			"when" => SyntaxKind.WhenKeyword,
			"nameof" => SyntaxKind.NameOfKeyword,
			"_" => SyntaxKind.UnderscoreToken,
			"var" => SyntaxKind.VarKeyword,
			"and" => SyntaxKind.AndKeyword,
			"or" => SyntaxKind.OrKeyword,
			"not" => SyntaxKind.NotKeyword,
			"with" => SyntaxKind.WithKeyword,
			"init" => SyntaxKind.InitKeyword,
			"record" => SyntaxKind.RecordKeyword,
			"managed" => SyntaxKind.ManagedKeyword,
			"unmanaged" => SyntaxKind.UnmanagedKeyword,
			"required" => SyntaxKind.RequiredKeyword,
			"scoped" => SyntaxKind.ScopedKeyword,
			"file" => SyntaxKind.FileKeyword,
			"allows" => SyntaxKind.AllowsKeyword,
			"extension" => SyntaxKind.ExtensionKeyword,
			_ => SyntaxKind.None,
		};
	}

	public static string GetText(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.TildeToken => "~",
			SyntaxKind.ExclamationToken => "!",
			SyntaxKind.DollarToken => "$",
			SyntaxKind.PercentToken => "%",
			SyntaxKind.CaretToken => "^",
			SyntaxKind.AmpersandToken => "&",
			SyntaxKind.AsteriskToken => "*",
			SyntaxKind.OpenParenToken => "(",
			SyntaxKind.CloseParenToken => ")",
			SyntaxKind.MinusToken => "-",
			SyntaxKind.PlusToken => "+",
			SyntaxKind.EqualsToken => "=",
			SyntaxKind.OpenBraceToken => "{",
			SyntaxKind.CloseBraceToken => "}",
			SyntaxKind.OpenBracketToken => "[",
			SyntaxKind.CloseBracketToken => "]",
			SyntaxKind.BarToken => "|",
			SyntaxKind.BackslashToken => "\\",
			SyntaxKind.ColonToken => ":",
			SyntaxKind.SemicolonToken => ";",
			SyntaxKind.DoubleQuoteToken => "\"",
			SyntaxKind.SingleQuoteToken => "'",
			SyntaxKind.LessThanToken => "<",
			SyntaxKind.CommaToken => ",",
			SyntaxKind.GreaterThanToken => ">",
			SyntaxKind.DotToken => ".",
			SyntaxKind.QuestionToken => "?",
			SyntaxKind.HashToken => "#",
			SyntaxKind.SlashToken => "/",
			SyntaxKind.SlashGreaterThanToken => "/>",
			SyntaxKind.LessThanSlashToken => "</",
			SyntaxKind.XmlCommentStartToken => "<!--",
			SyntaxKind.XmlCommentEndToken => "-->",
			SyntaxKind.XmlCDataStartToken => "<![CDATA[",
			SyntaxKind.XmlCDataEndToken => "]]>",
			SyntaxKind.XmlProcessingInstructionStartToken => "<?",
			SyntaxKind.XmlProcessingInstructionEndToken => "?>",
			SyntaxKind.BarBarToken => "||",
			SyntaxKind.AmpersandAmpersandToken => "&&",
			SyntaxKind.MinusMinusToken => "--",
			SyntaxKind.PlusPlusToken => "++",
			SyntaxKind.ColonColonToken => "::",
			SyntaxKind.QuestionQuestionToken => "??",
			SyntaxKind.MinusGreaterThanToken => "->",
			SyntaxKind.ExclamationEqualsToken => "!=",
			SyntaxKind.EqualsEqualsToken => "==",
			SyntaxKind.EqualsGreaterThanToken => "=>",
			SyntaxKind.LessThanEqualsToken => "<=",
			SyntaxKind.LessThanLessThanToken => "<<",
			SyntaxKind.LessThanLessThanEqualsToken => "<<=",
			SyntaxKind.GreaterThanEqualsToken => ">=",
			SyntaxKind.GreaterThanGreaterThanToken => ">>",
			SyntaxKind.GreaterThanGreaterThanEqualsToken => ">>=",
			SyntaxKind.GreaterThanGreaterThanGreaterThanToken => ">>>",
			SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken => ">>>=",
			SyntaxKind.SlashEqualsToken => "/=",
			SyntaxKind.AsteriskEqualsToken => "*=",
			SyntaxKind.BarEqualsToken => "|=",
			SyntaxKind.AmpersandEqualsToken => "&=",
			SyntaxKind.PlusEqualsToken => "+=",
			SyntaxKind.MinusEqualsToken => "-=",
			SyntaxKind.CaretEqualsToken => "^=",
			SyntaxKind.PercentEqualsToken => "%=",
			SyntaxKind.QuestionQuestionEqualsToken => "??=",
			SyntaxKind.DotDotToken => "..",
			SyntaxKind.BoolKeyword => "bool",
			SyntaxKind.ByteKeyword => "byte",
			SyntaxKind.SByteKeyword => "sbyte",
			SyntaxKind.ShortKeyword => "short",
			SyntaxKind.UShortKeyword => "ushort",
			SyntaxKind.IntKeyword => "int",
			SyntaxKind.UIntKeyword => "uint",
			SyntaxKind.LongKeyword => "long",
			SyntaxKind.ULongKeyword => "ulong",
			SyntaxKind.DoubleKeyword => "double",
			SyntaxKind.FloatKeyword => "float",
			SyntaxKind.DecimalKeyword => "decimal",
			SyntaxKind.StringKeyword => "string",
			SyntaxKind.CharKeyword => "char",
			SyntaxKind.VoidKeyword => "void",
			SyntaxKind.ObjectKeyword => "object",
			SyntaxKind.TypeOfKeyword => "typeof",
			SyntaxKind.SizeOfKeyword => "sizeof",
			SyntaxKind.NullKeyword => "null",
			SyntaxKind.TrueKeyword => "true",
			SyntaxKind.FalseKeyword => "false",
			SyntaxKind.IfKeyword => "if",
			SyntaxKind.ElseKeyword => "else",
			SyntaxKind.WhileKeyword => "while",
			SyntaxKind.ForKeyword => "for",
			SyntaxKind.ForEachKeyword => "foreach",
			SyntaxKind.DoKeyword => "do",
			SyntaxKind.SwitchKeyword => "switch",
			SyntaxKind.CaseKeyword => "case",
			SyntaxKind.DefaultKeyword => "default",
			SyntaxKind.TryKeyword => "try",
			SyntaxKind.CatchKeyword => "catch",
			SyntaxKind.FinallyKeyword => "finally",
			SyntaxKind.LockKeyword => "lock",
			SyntaxKind.GotoKeyword => "goto",
			SyntaxKind.BreakKeyword => "break",
			SyntaxKind.ContinueKeyword => "continue",
			SyntaxKind.ReturnKeyword => "return",
			SyntaxKind.ThrowKeyword => "throw",
			SyntaxKind.PublicKeyword => "public",
			SyntaxKind.PrivateKeyword => "private",
			SyntaxKind.InternalKeyword => "internal",
			SyntaxKind.ProtectedKeyword => "protected",
			SyntaxKind.StaticKeyword => "static",
			SyntaxKind.ReadOnlyKeyword => "readonly",
			SyntaxKind.SealedKeyword => "sealed",
			SyntaxKind.ConstKeyword => "const",
			SyntaxKind.FixedKeyword => "fixed",
			SyntaxKind.StackAllocKeyword => "stackalloc",
			SyntaxKind.VolatileKeyword => "volatile",
			SyntaxKind.NewKeyword => "new",
			SyntaxKind.OverrideKeyword => "override",
			SyntaxKind.AbstractKeyword => "abstract",
			SyntaxKind.VirtualKeyword => "virtual",
			SyntaxKind.EventKeyword => "event",
			SyntaxKind.ExternKeyword => "extern",
			SyntaxKind.RefKeyword => "ref",
			SyntaxKind.OutKeyword => "out",
			SyntaxKind.InKeyword => "in",
			SyntaxKind.IsKeyword => "is",
			SyntaxKind.AsKeyword => "as",
			SyntaxKind.ParamsKeyword => "params",
			SyntaxKind.ArgListKeyword => "__arglist",
			SyntaxKind.MakeRefKeyword => "__makeref",
			SyntaxKind.RefTypeKeyword => "__reftype",
			SyntaxKind.RefValueKeyword => "__refvalue",
			SyntaxKind.ThisKeyword => "this",
			SyntaxKind.BaseKeyword => "base",
			SyntaxKind.NamespaceKeyword => "namespace",
			SyntaxKind.UsingKeyword => "using",
			SyntaxKind.ClassKeyword => "class",
			SyntaxKind.StructKeyword => "struct",
			SyntaxKind.InterfaceKeyword => "interface",
			SyntaxKind.EnumKeyword => "enum",
			SyntaxKind.DelegateKeyword => "delegate",
			SyntaxKind.CheckedKeyword => "checked",
			SyntaxKind.UncheckedKeyword => "unchecked",
			SyntaxKind.UnsafeKeyword => "unsafe",
			SyntaxKind.OperatorKeyword => "operator",
			SyntaxKind.ImplicitKeyword => "implicit",
			SyntaxKind.ExplicitKeyword => "explicit",
			SyntaxKind.ElifKeyword => "elif",
			SyntaxKind.EndIfKeyword => "endif",
			SyntaxKind.RegionKeyword => "region",
			SyntaxKind.EndRegionKeyword => "endregion",
			SyntaxKind.DefineKeyword => "define",
			SyntaxKind.UndefKeyword => "undef",
			SyntaxKind.WarningKeyword => "warning",
			SyntaxKind.ErrorKeyword => "error",
			SyntaxKind.LineKeyword => "line",
			SyntaxKind.PragmaKeyword => "pragma",
			SyntaxKind.HiddenKeyword => "hidden",
			SyntaxKind.ChecksumKeyword => "checksum",
			SyntaxKind.DisableKeyword => "disable",
			SyntaxKind.RestoreKeyword => "restore",
			SyntaxKind.ReferenceKeyword => "r",
			SyntaxKind.LoadKeyword => "load",
			SyntaxKind.NullableKeyword => "nullable",
			SyntaxKind.EnableKeyword => "enable",
			SyntaxKind.WarningsKeyword => "warnings",
			SyntaxKind.AnnotationsKeyword => "annotations",
			SyntaxKind.YieldKeyword => "yield",
			SyntaxKind.PartialKeyword => "partial",
			SyntaxKind.FromKeyword => "from",
			SyntaxKind.GroupKeyword => "group",
			SyntaxKind.JoinKeyword => "join",
			SyntaxKind.IntoKeyword => "into",
			SyntaxKind.LetKeyword => "let",
			SyntaxKind.ByKeyword => "by",
			SyntaxKind.WhereKeyword => "where",
			SyntaxKind.SelectKeyword => "select",
			SyntaxKind.GetKeyword => "get",
			SyntaxKind.SetKeyword => "set",
			SyntaxKind.AddKeyword => "add",
			SyntaxKind.RemoveKeyword => "remove",
			SyntaxKind.OrderByKeyword => "orderby",
			SyntaxKind.AliasKeyword => "alias",
			SyntaxKind.OnKeyword => "on",
			SyntaxKind.EqualsKeyword => "equals",
			SyntaxKind.AscendingKeyword => "ascending",
			SyntaxKind.DescendingKeyword => "descending",
			SyntaxKind.AssemblyKeyword => "assembly",
			SyntaxKind.ModuleKeyword => "module",
			SyntaxKind.TypeKeyword => "type",
			SyntaxKind.FieldKeyword => "field",
			SyntaxKind.MethodKeyword => "method",
			SyntaxKind.ParamKeyword => "param",
			SyntaxKind.PropertyKeyword => "property",
			SyntaxKind.TypeVarKeyword => "typevar",
			SyntaxKind.GlobalKeyword => "global",
			SyntaxKind.NameOfKeyword => "nameof",
			SyntaxKind.AsyncKeyword => "async",
			SyntaxKind.AwaitKeyword => "await",
			SyntaxKind.WhenKeyword => "when",
			SyntaxKind.InterpolatedStringStartToken => "$\"",
			SyntaxKind.InterpolatedStringEndToken => "\"",
			SyntaxKind.InterpolatedVerbatimStringStartToken => "$@\"",
			SyntaxKind.UnderscoreToken => "_",
			SyntaxKind.VarKeyword => "var",
			SyntaxKind.AndKeyword => "and",
			SyntaxKind.OrKeyword => "or",
			SyntaxKind.NotKeyword => "not",
			SyntaxKind.WithKeyword => "with",
			SyntaxKind.InitKeyword => "init",
			SyntaxKind.RecordKeyword => "record",
			SyntaxKind.ManagedKeyword => "managed",
			SyntaxKind.UnmanagedKeyword => "unmanaged",
			SyntaxKind.RequiredKeyword => "required",
			SyntaxKind.ScopedKeyword => "scoped",
			SyntaxKind.FileKeyword => "file",
			SyntaxKind.AllowsKeyword => "allows",
			SyntaxKind.ExtensionKeyword => "extension",
			_ => string.Empty,
		};
	}

	public static string GetText(Accessibility accessibility)
	{
		return (int)accessibility switch
		{
			0 => string.Empty,
			1 => GetText(SyntaxKind.PrivateKeyword),
			2 => GetText(SyntaxKind.PrivateKeyword) + " " + GetText(SyntaxKind.ProtectedKeyword),
			4 => GetText(SyntaxKind.InternalKeyword),
			3 => GetText(SyntaxKind.ProtectedKeyword),
			5 => GetText(SyntaxKind.ProtectedKeyword) + " " + GetText(SyntaxKind.InternalKeyword),
			6 => GetText(SyntaxKind.PublicKeyword),
			_ => throw new ArgumentOutOfRangeException(nameof(accessibility)),
		};
	}

	public static SyntaxKind GetOperatorKind(string operatorMetadataName)
	{
		switch (operatorMetadataName)
		{
		case "op_CheckedAddition":
		case "op_Addition":
			return SyntaxKind.PlusToken;
		case "op_BitwiseAnd":
			return SyntaxKind.AmpersandToken;
		case "op_BitwiseOr":
			return SyntaxKind.BarToken;
		case "op_Decrement":
		case "op_CheckedDecrement":
		case "op_CheckedDecrementAssignment":
		case "op_DecrementAssignment":
			return SyntaxKind.MinusMinusToken;
		case "op_CheckedDivision":
		case "op_Division":
			return SyntaxKind.SlashToken;
		case "op_Equality":
			return SyntaxKind.EqualsEqualsToken;
		case "op_ExclusiveOr":
			return SyntaxKind.CaretToken;
		case "op_CheckedExplicit":
		case "op_Explicit":
			return SyntaxKind.ExplicitKeyword;
		case "op_False":
			return SyntaxKind.FalseKeyword;
		case "op_GreaterThan":
			return SyntaxKind.GreaterThanToken;
		case "op_GreaterThanOrEqual":
			return SyntaxKind.GreaterThanEqualsToken;
		case "op_Implicit":
			return SyntaxKind.ImplicitKeyword;
		case "op_Increment":
		case "op_CheckedIncrement":
		case "op_CheckedIncrementAssignment":
		case "op_IncrementAssignment":
			return SyntaxKind.PlusPlusToken;
		case "op_Inequality":
			return SyntaxKind.ExclamationEqualsToken;
		case "op_LeftShift":
			return SyntaxKind.LessThanLessThanToken;
		case "op_LessThan":
			return SyntaxKind.LessThanToken;
		case "op_LessThanOrEqual":
			return SyntaxKind.LessThanEqualsToken;
		case "op_LogicalNot":
			return SyntaxKind.ExclamationToken;
		case "op_Modulus":
			return SyntaxKind.PercentToken;
		case "op_CheckedMultiply":
		case "op_Multiply":
			return SyntaxKind.AsteriskToken;
		case "op_OnesComplement":
			return SyntaxKind.TildeToken;
		case "op_RightShift":
			return SyntaxKind.GreaterThanGreaterThanToken;
		case "op_UnsignedRightShift":
			return SyntaxKind.GreaterThanGreaterThanGreaterThanToken;
		case "op_Subtraction":
		case "op_CheckedSubtraction":
			return SyntaxKind.MinusToken;
		case "op_True":
			return SyntaxKind.TrueKeyword;
		case "op_CheckedUnaryNegation":
		case "op_UnaryNegation":
			return SyntaxKind.MinusToken;
		case "op_UnaryPlus":
			return SyntaxKind.PlusToken;
		case "op_AdditionAssignment":
		case "op_CheckedAdditionAssignment":
			return SyntaxKind.PlusEqualsToken;
		case "op_DivisionAssignment":
		case "op_CheckedDivisionAssignment":
			return SyntaxKind.SlashEqualsToken;
		case "op_CheckedMultiplicationAssignment":
		case "op_MultiplicationAssignment":
			return SyntaxKind.AsteriskEqualsToken;
		case "op_CheckedSubtractionAssignment":
		case "op_SubtractionAssignment":
			return SyntaxKind.MinusEqualsToken;
		case "op_ModulusAssignment":
			return SyntaxKind.PercentEqualsToken;
		case "op_BitwiseAndAssignment":
			return SyntaxKind.AmpersandEqualsToken;
		case "op_BitwiseOrAssignment":
			return SyntaxKind.BarEqualsToken;
		case "op_ExclusiveOrAssignment":
			return SyntaxKind.CaretEqualsToken;
		case "op_LeftShiftAssignment":
			return SyntaxKind.LessThanLessThanEqualsToken;
		case "op_RightShiftAssignment":
			return SyntaxKind.GreaterThanGreaterThanEqualsToken;
		case "op_UnsignedRightShiftAssignment":
			return SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken;
		default:
			return SyntaxKind.None;
		}
	}

	public static bool IsCheckedOperator(string operatorMetadataName)
	{
		switch (operatorMetadataName)
		{
		case "op_CheckedDecrement":
		case "op_CheckedIncrement":
		case "op_CheckedAddition":
		case "op_CheckedDivision":
		case "op_CheckedMultiply":
		case "op_CheckedExplicit":
		case "op_CheckedAdditionAssignment":
		case "op_CheckedDivisionAssignment":
		case "op_CheckedDecrementAssignment":
		case "op_CheckedIncrementAssignment":
		case "op_CheckedUnaryNegation":
		case "op_CheckedSubtraction":
		case "op_CheckedMultiplicationAssignment":
		case "op_CheckedSubtractionAssignment":
			return true;
		default:
			return false;
		}
	}

	// ── Misc ────────────────────────────────────────────────────────────

	public static bool IsTypeParameterVarianceKeyword(SyntaxKind kind)
	{
		if (kind != SyntaxKind.OutKeyword)
		{
			return kind == SyntaxKind.InKeyword;
		}
		return true;
	}

	public static bool IsDocumentationCommentTrivia(SyntaxKind kind)
	{
		if (kind != SyntaxKind.SingleLineDocumentationCommentTrivia)
		{
			return kind == SyntaxKind.MultiLineDocumentationCommentTrivia;
		}
		return true;
	}
}
