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
		if (c >= '0' && c <= '9') return c - 48;
		if (c >= 'a' && c <= 'f') return c - 97 + 10;
		if (c >= 'A' && c <= 'F') return c - 65 + 10;
		return 0;
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
		if (kind == SyntaxKind.TrueKeyword || kind == SyntaxKind.FalseKeyword
			|| kind == SyntaxKind.IfKeyword || kind == SyntaxKind.ElseKeyword
			|| kind == SyntaxKind.DefaultKeyword || kind == SyntaxKind.ElifKeyword
			|| kind == SyntaxKind.EndIfKeyword || kind == SyntaxKind.RegionKeyword
			|| kind == SyntaxKind.EndRegionKeyword || kind == SyntaxKind.DefineKeyword
			|| kind == SyntaxKind.UndefKeyword || kind == SyntaxKind.WarningKeyword
			|| kind == SyntaxKind.ErrorKeyword || kind == SyntaxKind.LineKeyword
			|| kind == SyntaxKind.PragmaKeyword || kind == SyntaxKind.HiddenKeyword
			|| kind == SyntaxKind.ChecksumKeyword || kind == SyntaxKind.DisableKeyword
			|| kind == SyntaxKind.RestoreKeyword || kind == SyntaxKind.ReferenceKeyword
			|| kind == SyntaxKind.LoadKeyword || kind == SyntaxKind.NullableKeyword
			|| kind == SyntaxKind.EnableKeyword || kind == SyntaxKind.WarningsKeyword
			|| kind == SyntaxKind.AnnotationsKeyword)
		{
			return true;
		}
		return false;
	}

	/// <summary>
	/// Some preprocessor keywords are only keywords when they appear after a
	/// hash sign (#).  For these keywords, the lexer will produce tokens with
	/// Kind = SyntaxKind.IdentifierToken and ContextualKind set to the keyword
	/// SyntaxKind.
	/// </summary>
	internal static bool IsPreprocessorContextualKeyword(SyntaxKind kind)
	{
		if (kind == SyntaxKind.TrueKeyword || kind == SyntaxKind.FalseKeyword
			|| kind == SyntaxKind.DefaultKeyword || kind == SyntaxKind.HiddenKeyword
			|| kind == SyntaxKind.ChecksumKeyword || kind == SyntaxKind.DisableKeyword
			|| kind == SyntaxKind.RestoreKeyword || kind == SyntaxKind.EnableKeyword
			|| kind == SyntaxKind.WarningsKeyword || kind == SyntaxKind.AnnotationsKeyword)
		{
			return false;
		}
		return IsPreprocessorKeyword(kind);
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
		if (kind == SyntaxKind.InterpolatedStringStartToken
			|| kind == SyntaxKind.InterpolatedStringEndToken
			|| kind == SyntaxKind.InterpolatedVerbatimStringStartToken
			|| kind == SyntaxKind.LoadKeyword
			|| kind == SyntaxKind.NullableKeyword
			|| kind == SyntaxKind.EnableKeyword
			|| kind == SyntaxKind.UnderscoreToken
			|| kind == SyntaxKind.InterpolatedStringToken
			|| kind == SyntaxKind.InterpolatedStringTextToken
			|| kind == SyntaxKind.SingleLineRawStringLiteralToken
			|| kind == SyntaxKind.MultiLineRawStringLiteralToken
			|| kind == SyntaxKind.InterpolatedSingleLineRawStringStartToken
			|| kind == SyntaxKind.InterpolatedMultiLineRawStringStartToken
			|| kind == SyntaxKind.InterpolatedRawStringEndToken)
		{
			return true;
		}
		return false;
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
		if (kind == SyntaxKind.IfDirectiveTrivia
			|| kind == SyntaxKind.ElifDirectiveTrivia
			|| kind == SyntaxKind.ElseDirectiveTrivia
			|| kind == SyntaxKind.EndIfDirectiveTrivia
			|| kind == SyntaxKind.RegionDirectiveTrivia
			|| kind == SyntaxKind.EndRegionDirectiveTrivia
			|| kind == SyntaxKind.DefineDirectiveTrivia
			|| kind == SyntaxKind.UndefDirectiveTrivia
			|| kind == SyntaxKind.ErrorDirectiveTrivia
			|| kind == SyntaxKind.WarningDirectiveTrivia
			|| kind == SyntaxKind.LineDirectiveTrivia
			|| kind == SyntaxKind.PragmaWarningDirectiveTrivia
			|| kind == SyntaxKind.PragmaChecksumDirectiveTrivia
			|| kind == SyntaxKind.ReferenceDirectiveTrivia
			|| kind == SyntaxKind.BadDirectiveTrivia
			|| kind == SyntaxKind.ShebangDirectiveTrivia
			|| kind == SyntaxKind.LoadDirectiveTrivia
			|| kind == SyntaxKind.NullableDirectiveTrivia
			|| kind == SyntaxKind.LineSpanDirectiveTrivia
			|| kind == SyntaxKind.IgnoredDirectiveTrivia)
		{
			return true;
		}
		return false;
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
		if (token == SyntaxKind.PlusToken) return SyntaxKind.UnaryPlusExpression;
		if (token == SyntaxKind.MinusToken) return SyntaxKind.UnaryMinusExpression;
		if (token == SyntaxKind.TildeToken) return SyntaxKind.BitwiseNotExpression;
		if (token == SyntaxKind.ExclamationToken) return SyntaxKind.LogicalNotExpression;
		if (token == SyntaxKind.PlusPlusToken) return SyntaxKind.PreIncrementExpression;
		if (token == SyntaxKind.MinusMinusToken) return SyntaxKind.PreDecrementExpression;
		if (token == SyntaxKind.AmpersandToken) return SyntaxKind.AddressOfExpression;
		if (token == SyntaxKind.AsteriskToken) return SyntaxKind.PointerIndirectionExpression;
		if (token == SyntaxKind.CaretToken) return SyntaxKind.IndexExpression;
		return SyntaxKind.None;
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
		if (token == SyntaxKind.PlusPlusToken) return SyntaxKind.PostIncrementExpression;
		if (token == SyntaxKind.MinusMinusToken) return SyntaxKind.PostDecrementExpression;
		if (token == SyntaxKind.ExclamationToken) return SyntaxKind.SuppressNullableWarningExpression;
		return SyntaxKind.None;
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
		if (kind == SyntaxKind.PercentToken
			|| kind == SyntaxKind.CaretToken
			|| kind == SyntaxKind.AmpersandToken
			|| kind == SyntaxKind.AsteriskToken
			|| kind == SyntaxKind.MinusToken
			|| kind == SyntaxKind.PlusToken
			|| kind == SyntaxKind.BarToken
			|| kind == SyntaxKind.LessThanToken
			|| kind == SyntaxKind.GreaterThanToken
			|| kind == SyntaxKind.SlashToken
			|| kind == SyntaxKind.ExclamationEqualsToken
			|| kind == SyntaxKind.EqualsEqualsToken
			|| kind == SyntaxKind.LessThanEqualsToken
			|| kind == SyntaxKind.LessThanLessThanToken
			|| kind == SyntaxKind.GreaterThanEqualsToken
			|| kind == SyntaxKind.GreaterThanGreaterThanToken
			|| kind == SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
		{
			return true;
		}
		return false;
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
		if (keyword == SyntaxKind.MakeRefKeyword) return SyntaxKind.MakeRefExpression;
		if (keyword == SyntaxKind.RefTypeKeyword) return SyntaxKind.RefTypeExpression;
		if (keyword == SyntaxKind.RefValueKeyword) return SyntaxKind.RefValueExpression;
		if (keyword == SyntaxKind.CheckedKeyword) return SyntaxKind.CheckedExpression;
		if (keyword == SyntaxKind.UncheckedKeyword) return SyntaxKind.UncheckedExpression;
		if (keyword == SyntaxKind.DefaultKeyword) return SyntaxKind.DefaultExpression;
		if (keyword == SyntaxKind.TypeOfKeyword) return SyntaxKind.TypeOfExpression;
		if (keyword == SyntaxKind.SizeOfKeyword) return SyntaxKind.SizeOfExpression;
		return SyntaxKind.None;
	}

	public static bool IsLiteralExpression(SyntaxKind token)
	{
		return GetLiteralExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetLiteralExpression(SyntaxKind token)
	{
		if (token == SyntaxKind.StringLiteralToken) return SyntaxKind.StringLiteralExpression;
		if (token == SyntaxKind.Utf8StringLiteralToken) return SyntaxKind.Utf8StringLiteralExpression;
		if (token == SyntaxKind.SingleLineRawStringLiteralToken) return SyntaxKind.StringLiteralExpression;
		if (token == SyntaxKind.Utf8SingleLineRawStringLiteralToken) return SyntaxKind.Utf8StringLiteralExpression;
		if (token == SyntaxKind.MultiLineRawStringLiteralToken) return SyntaxKind.StringLiteralExpression;
		if (token == SyntaxKind.Utf8MultiLineRawStringLiteralToken) return SyntaxKind.Utf8StringLiteralExpression;
		if (token == SyntaxKind.CharacterLiteralToken) return SyntaxKind.CharacterLiteralExpression;
		if (token == SyntaxKind.NumericLiteralToken) return SyntaxKind.NumericLiteralExpression;
		if (token == SyntaxKind.NullKeyword) return SyntaxKind.NullLiteralExpression;
		if (token == SyntaxKind.TrueKeyword) return SyntaxKind.TrueLiteralExpression;
		if (token == SyntaxKind.FalseKeyword) return SyntaxKind.FalseLiteralExpression;
		if (token == SyntaxKind.ArgListKeyword) return SyntaxKind.ArgListExpression;
		return SyntaxKind.None;
	}

	public static bool IsInstanceExpression(SyntaxKind token)
	{
		return GetInstanceExpression(token) != SyntaxKind.None;
	}

	public static SyntaxKind GetInstanceExpression(SyntaxKind token)
	{
		if (token == SyntaxKind.ThisKeyword) return SyntaxKind.ThisExpression;
		if (token == SyntaxKind.BaseKeyword) return SyntaxKind.BaseExpression;
		return SyntaxKind.None;
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
		if (token == SyntaxKind.QuestionQuestionToken) return SyntaxKind.CoalesceExpression;
		if (token == SyntaxKind.IsKeyword) return SyntaxKind.IsExpression;
		if (token == SyntaxKind.AsKeyword) return SyntaxKind.AsExpression;
		if (token == SyntaxKind.BarToken) return SyntaxKind.BitwiseOrExpression;
		if (token == SyntaxKind.CaretToken) return SyntaxKind.ExclusiveOrExpression;
		if (token == SyntaxKind.AmpersandToken) return SyntaxKind.BitwiseAndExpression;
		if (token == SyntaxKind.EqualsEqualsToken) return SyntaxKind.EqualsExpression;
		if (token == SyntaxKind.ExclamationEqualsToken) return SyntaxKind.NotEqualsExpression;
		if (token == SyntaxKind.LessThanToken) return SyntaxKind.LessThanExpression;
		if (token == SyntaxKind.LessThanEqualsToken) return SyntaxKind.LessThanOrEqualExpression;
		if (token == SyntaxKind.GreaterThanToken) return SyntaxKind.GreaterThanExpression;
		if (token == SyntaxKind.GreaterThanEqualsToken) return SyntaxKind.GreaterThanOrEqualExpression;
		if (token == SyntaxKind.LessThanLessThanToken) return SyntaxKind.LeftShiftExpression;
		if (token == SyntaxKind.GreaterThanGreaterThanToken) return SyntaxKind.RightShiftExpression;
		if (token == SyntaxKind.GreaterThanGreaterThanGreaterThanToken) return SyntaxKind.UnsignedRightShiftExpression;
		if (token == SyntaxKind.PlusToken) return SyntaxKind.AddExpression;
		if (token == SyntaxKind.MinusToken) return SyntaxKind.SubtractExpression;
		if (token == SyntaxKind.AsteriskToken) return SyntaxKind.MultiplyExpression;
		if (token == SyntaxKind.SlashToken) return SyntaxKind.DivideExpression;
		if (token == SyntaxKind.PercentToken) return SyntaxKind.ModuloExpression;
		if (token == SyntaxKind.AmpersandAmpersandToken) return SyntaxKind.LogicalAndExpression;
		if (token == SyntaxKind.BarBarToken) return SyntaxKind.LogicalOrExpression;
		return SyntaxKind.None;
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
		if (token == SyntaxKind.EqualsToken
			|| token == SyntaxKind.LessThanLessThanEqualsToken
			|| token == SyntaxKind.GreaterThanGreaterThanEqualsToken
			|| token == SyntaxKind.SlashEqualsToken
			|| token == SyntaxKind.AsteriskEqualsToken
			|| token == SyntaxKind.BarEqualsToken
			|| token == SyntaxKind.AmpersandEqualsToken
			|| token == SyntaxKind.PlusEqualsToken
			|| token == SyntaxKind.MinusEqualsToken
			|| token == SyntaxKind.CaretEqualsToken
			|| token == SyntaxKind.PercentEqualsToken
			|| token == SyntaxKind.QuestionQuestionEqualsToken
			|| token == SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken)
		{
			return true;
		}
		return false;
	}

	public static SyntaxKind GetAssignmentExpression(SyntaxKind token)
	{
		if (token == SyntaxKind.BarEqualsToken) return SyntaxKind.OrAssignmentExpression;
		if (token == SyntaxKind.AmpersandEqualsToken) return SyntaxKind.AndAssignmentExpression;
		if (token == SyntaxKind.CaretEqualsToken) return SyntaxKind.ExclusiveOrAssignmentExpression;
		if (token == SyntaxKind.LessThanLessThanEqualsToken) return SyntaxKind.LeftShiftAssignmentExpression;
		if (token == SyntaxKind.GreaterThanGreaterThanEqualsToken) return SyntaxKind.RightShiftAssignmentExpression;
		if (token == SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken) return SyntaxKind.UnsignedRightShiftAssignmentExpression;
		if (token == SyntaxKind.PlusEqualsToken) return SyntaxKind.AddAssignmentExpression;
		if (token == SyntaxKind.MinusEqualsToken) return SyntaxKind.SubtractAssignmentExpression;
		if (token == SyntaxKind.AsteriskEqualsToken) return SyntaxKind.MultiplyAssignmentExpression;
		if (token == SyntaxKind.SlashEqualsToken) return SyntaxKind.DivideAssignmentExpression;
		if (token == SyntaxKind.PercentEqualsToken) return SyntaxKind.ModuloAssignmentExpression;
		if (token == SyntaxKind.EqualsToken) return SyntaxKind.SimpleAssignmentExpression;
		if (token == SyntaxKind.QuestionQuestionEqualsToken) return SyntaxKind.CoalesceAssignmentExpression;
		return SyntaxKind.None;
	}

	public static SyntaxKind GetCheckStatement(SyntaxKind keyword)
	{
		if (keyword == SyntaxKind.CheckedKeyword) return SyntaxKind.CheckedStatement;
		if (keyword == SyntaxKind.UncheckedKeyword) return SyntaxKind.UncheckedStatement;
		return SyntaxKind.None;
	}

	public static SyntaxKind GetAccessorDeclarationKind(SyntaxKind keyword)
	{
		if (keyword == SyntaxKind.GetKeyword) return SyntaxKind.GetAccessorDeclaration;
		if (keyword == SyntaxKind.SetKeyword) return SyntaxKind.SetAccessorDeclaration;
		if (keyword == SyntaxKind.InitKeyword) return SyntaxKind.InitAccessorDeclaration;
		if (keyword == SyntaxKind.AddKeyword) return SyntaxKind.AddAccessorDeclaration;
		if (keyword == SyntaxKind.RemoveKeyword) return SyntaxKind.RemoveAccessorDeclaration;
		return SyntaxKind.None;
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
		if (keyword == SyntaxKind.CaseKeyword) return SyntaxKind.CaseSwitchLabel;
		if (keyword == SyntaxKind.DefaultKeyword) return SyntaxKind.DefaultSwitchLabel;
		return SyntaxKind.None;
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
		if (kind == SyntaxKind.ClassKeyword) return SyntaxKind.ClassDeclaration;
		if (kind == SyntaxKind.StructKeyword) return SyntaxKind.StructDeclaration;
		if (kind == SyntaxKind.InterfaceKeyword) return SyntaxKind.InterfaceDeclaration;
		if (kind == SyntaxKind.RecordKeyword) return SyntaxKind.RecordDeclaration;
		return SyntaxKind.None;
	}

	// ── Keyword/text lookup ─────────────────────────────────────────────

	public static SyntaxKind GetKeywordKind(string text)
	{
		if (text == "bool") return SyntaxKind.BoolKeyword;
		if (text == "byte") return SyntaxKind.ByteKeyword;
		if (text == "sbyte") return SyntaxKind.SByteKeyword;
		if (text == "short") return SyntaxKind.ShortKeyword;
		if (text == "ushort") return SyntaxKind.UShortKeyword;
		if (text == "int") return SyntaxKind.IntKeyword;
		if (text == "uint") return SyntaxKind.UIntKeyword;
		if (text == "long") return SyntaxKind.LongKeyword;
		if (text == "ulong") return SyntaxKind.ULongKeyword;
		if (text == "double") return SyntaxKind.DoubleKeyword;
		if (text == "float") return SyntaxKind.FloatKeyword;
		if (text == "decimal") return SyntaxKind.DecimalKeyword;
		if (text == "string") return SyntaxKind.StringKeyword;
		if (text == "char") return SyntaxKind.CharKeyword;
		if (text == "void") return SyntaxKind.VoidKeyword;
		if (text == "object") return SyntaxKind.ObjectKeyword;
		if (text == "typeof") return SyntaxKind.TypeOfKeyword;
		if (text == "sizeof") return SyntaxKind.SizeOfKeyword;
		if (text == "null") return SyntaxKind.NullKeyword;
		if (text == "true") return SyntaxKind.TrueKeyword;
		if (text == "false") return SyntaxKind.FalseKeyword;
		if (text == "if") return SyntaxKind.IfKeyword;
		if (text == "else") return SyntaxKind.ElseKeyword;
		if (text == "while") return SyntaxKind.WhileKeyword;
		if (text == "for") return SyntaxKind.ForKeyword;
		if (text == "foreach") return SyntaxKind.ForEachKeyword;
		if (text == "do") return SyntaxKind.DoKeyword;
		if (text == "switch") return SyntaxKind.SwitchKeyword;
		if (text == "case") return SyntaxKind.CaseKeyword;
		if (text == "default") return SyntaxKind.DefaultKeyword;
		if (text == "lock") return SyntaxKind.LockKeyword;
		if (text == "try") return SyntaxKind.TryKeyword;
		if (text == "throw") return SyntaxKind.ThrowKeyword;
		if (text == "catch") return SyntaxKind.CatchKeyword;
		if (text == "finally") return SyntaxKind.FinallyKeyword;
		if (text == "goto") return SyntaxKind.GotoKeyword;
		if (text == "break") return SyntaxKind.BreakKeyword;
		if (text == "continue") return SyntaxKind.ContinueKeyword;
		if (text == "return") return SyntaxKind.ReturnKeyword;
		if (text == "public") return SyntaxKind.PublicKeyword;
		if (text == "private") return SyntaxKind.PrivateKeyword;
		if (text == "internal") return SyntaxKind.InternalKeyword;
		if (text == "protected") return SyntaxKind.ProtectedKeyword;
		if (text == "static") return SyntaxKind.StaticKeyword;
		if (text == "readonly") return SyntaxKind.ReadOnlyKeyword;
		if (text == "sealed") return SyntaxKind.SealedKeyword;
		if (text == "const") return SyntaxKind.ConstKeyword;
		if (text == "fixed") return SyntaxKind.FixedKeyword;
		if (text == "stackalloc") return SyntaxKind.StackAllocKeyword;
		if (text == "volatile") return SyntaxKind.VolatileKeyword;
		if (text == "new") return SyntaxKind.NewKeyword;
		if (text == "override") return SyntaxKind.OverrideKeyword;
		if (text == "abstract") return SyntaxKind.AbstractKeyword;
		if (text == "virtual") return SyntaxKind.VirtualKeyword;
		if (text == "event") return SyntaxKind.EventKeyword;
		if (text == "extern") return SyntaxKind.ExternKeyword;
		if (text == "ref") return SyntaxKind.RefKeyword;
		if (text == "out") return SyntaxKind.OutKeyword;
		if (text == "in") return SyntaxKind.InKeyword;
		if (text == "is") return SyntaxKind.IsKeyword;
		if (text == "as") return SyntaxKind.AsKeyword;
		if (text == "params") return SyntaxKind.ParamsKeyword;
		if (text == "__arglist") return SyntaxKind.ArgListKeyword;
		if (text == "__makeref") return SyntaxKind.MakeRefKeyword;
		if (text == "__reftype") return SyntaxKind.RefTypeKeyword;
		if (text == "__refvalue") return SyntaxKind.RefValueKeyword;
		if (text == "this") return SyntaxKind.ThisKeyword;
		if (text == "base") return SyntaxKind.BaseKeyword;
		if (text == "namespace") return SyntaxKind.NamespaceKeyword;
		if (text == "using") return SyntaxKind.UsingKeyword;
		if (text == "class") return SyntaxKind.ClassKeyword;
		if (text == "struct") return SyntaxKind.StructKeyword;
		if (text == "interface") return SyntaxKind.InterfaceKeyword;
		if (text == "enum") return SyntaxKind.EnumKeyword;
		if (text == "delegate") return SyntaxKind.DelegateKeyword;
		if (text == "checked") return SyntaxKind.CheckedKeyword;
		if (text == "unchecked") return SyntaxKind.UncheckedKeyword;
		if (text == "unsafe") return SyntaxKind.UnsafeKeyword;
		if (text == "operator") return SyntaxKind.OperatorKeyword;
		if (text == "implicit") return SyntaxKind.ImplicitKeyword;
		if (text == "explicit") return SyntaxKind.ExplicitKeyword;
		return SyntaxKind.None;
	}

	public static SyntaxKind GetPreprocessorKeywordKind(string text)
	{
		if (text == "true") return SyntaxKind.TrueKeyword;
		if (text == "false") return SyntaxKind.FalseKeyword;
		if (text == "default") return SyntaxKind.DefaultKeyword;
		if (text == "if") return SyntaxKind.IfKeyword;
		if (text == "else") return SyntaxKind.ElseKeyword;
		if (text == "elif") return SyntaxKind.ElifKeyword;
		if (text == "endif") return SyntaxKind.EndIfKeyword;
		if (text == "region") return SyntaxKind.RegionKeyword;
		if (text == "endregion") return SyntaxKind.EndRegionKeyword;
		if (text == "define") return SyntaxKind.DefineKeyword;
		if (text == "undef") return SyntaxKind.UndefKeyword;
		if (text == "warning") return SyntaxKind.WarningKeyword;
		if (text == "error") return SyntaxKind.ErrorKeyword;
		if (text == "line") return SyntaxKind.LineKeyword;
		if (text == "pragma") return SyntaxKind.PragmaKeyword;
		if (text == "hidden") return SyntaxKind.HiddenKeyword;
		if (text == "checksum") return SyntaxKind.ChecksumKeyword;
		if (text == "disable") return SyntaxKind.DisableKeyword;
		if (text == "restore") return SyntaxKind.RestoreKeyword;
		if (text == "r") return SyntaxKind.ReferenceKeyword;
		if (text == "load") return SyntaxKind.LoadKeyword;
		if (text == "nullable") return SyntaxKind.NullableKeyword;
		if (text == "enable") return SyntaxKind.EnableKeyword;
		if (text == "warnings") return SyntaxKind.WarningsKeyword;
		if (text == "annotations") return SyntaxKind.AnnotationsKeyword;
		return SyntaxKind.None;
	}

	public static bool IsContextualKeyword(SyntaxKind kind)
	{
		if (kind == SyntaxKind.YieldKeyword
			|| kind == SyntaxKind.PartialKeyword
			|| kind == SyntaxKind.AliasKeyword
			|| kind == SyntaxKind.GlobalKeyword
			|| kind == SyntaxKind.AssemblyKeyword
			|| kind == SyntaxKind.ModuleKeyword
			|| kind == SyntaxKind.TypeKeyword
			|| kind == SyntaxKind.FieldKeyword
			|| kind == SyntaxKind.MethodKeyword
			|| kind == SyntaxKind.ParamKeyword
			|| kind == SyntaxKind.PropertyKeyword
			|| kind == SyntaxKind.TypeVarKeyword
			|| kind == SyntaxKind.GetKeyword
			|| kind == SyntaxKind.SetKeyword
			|| kind == SyntaxKind.AddKeyword
			|| kind == SyntaxKind.RemoveKeyword
			|| kind == SyntaxKind.WhereKeyword
			|| kind == SyntaxKind.FromKeyword
			|| kind == SyntaxKind.GroupKeyword
			|| kind == SyntaxKind.JoinKeyword
			|| kind == SyntaxKind.IntoKeyword
			|| kind == SyntaxKind.LetKeyword
			|| kind == SyntaxKind.ByKeyword
			|| kind == SyntaxKind.SelectKeyword
			|| kind == SyntaxKind.OrderByKeyword
			|| kind == SyntaxKind.OnKeyword
			|| kind == SyntaxKind.EqualsKeyword
			|| kind == SyntaxKind.AscendingKeyword
			|| kind == SyntaxKind.DescendingKeyword
			|| kind == SyntaxKind.NameOfKeyword
			|| kind == SyntaxKind.AsyncKeyword
			|| kind == SyntaxKind.AwaitKeyword
			|| kind == SyntaxKind.WhenKeyword
			|| kind == SyntaxKind.OrKeyword
			|| kind == SyntaxKind.AndKeyword
			|| kind == SyntaxKind.NotKeyword
			|| kind == SyntaxKind.WithKeyword
			|| kind == SyntaxKind.InitKeyword
			|| kind == SyntaxKind.RecordKeyword
			|| kind == SyntaxKind.ManagedKeyword
			|| kind == SyntaxKind.UnmanagedKeyword
			|| kind == SyntaxKind.RequiredKeyword
			|| kind == SyntaxKind.ScopedKeyword
			|| kind == SyntaxKind.FileKeyword
			|| kind == SyntaxKind.AllowsKeyword
			|| kind == SyntaxKind.ExtensionKeyword
			|| kind == SyntaxKind.VarKeyword
			|| kind == SyntaxKind.UnderscoreToken)
		{
			return true;
		}
		return false;
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
		if (text == "yield") return SyntaxKind.YieldKeyword;
		if (text == "partial") return SyntaxKind.PartialKeyword;
		if (text == "from") return SyntaxKind.FromKeyword;
		if (text == "group") return SyntaxKind.GroupKeyword;
		if (text == "join") return SyntaxKind.JoinKeyword;
		if (text == "into") return SyntaxKind.IntoKeyword;
		if (text == "let") return SyntaxKind.LetKeyword;
		if (text == "by") return SyntaxKind.ByKeyword;
		if (text == "where") return SyntaxKind.WhereKeyword;
		if (text == "select") return SyntaxKind.SelectKeyword;
		if (text == "get") return SyntaxKind.GetKeyword;
		if (text == "set") return SyntaxKind.SetKeyword;
		if (text == "add") return SyntaxKind.AddKeyword;
		if (text == "remove") return SyntaxKind.RemoveKeyword;
		if (text == "orderby") return SyntaxKind.OrderByKeyword;
		if (text == "alias") return SyntaxKind.AliasKeyword;
		if (text == "on") return SyntaxKind.OnKeyword;
		if (text == "equals") return SyntaxKind.EqualsKeyword;
		if (text == "ascending") return SyntaxKind.AscendingKeyword;
		if (text == "descending") return SyntaxKind.DescendingKeyword;
		if (text == "assembly") return SyntaxKind.AssemblyKeyword;
		if (text == "module") return SyntaxKind.ModuleKeyword;
		if (text == "type") return SyntaxKind.TypeKeyword;
		if (text == "field") return SyntaxKind.FieldKeyword;
		if (text == "method") return SyntaxKind.MethodKeyword;
		if (text == "param") return SyntaxKind.ParamKeyword;
		if (text == "property") return SyntaxKind.PropertyKeyword;
		if (text == "typevar") return SyntaxKind.TypeVarKeyword;
		if (text == "global") return SyntaxKind.GlobalKeyword;
		if (text == "async") return SyntaxKind.AsyncKeyword;
		if (text == "await") return SyntaxKind.AwaitKeyword;
		if (text == "when") return SyntaxKind.WhenKeyword;
		if (text == "nameof") return SyntaxKind.NameOfKeyword;
		if (text == "_") return SyntaxKind.UnderscoreToken;
		if (text == "var") return SyntaxKind.VarKeyword;
		if (text == "and") return SyntaxKind.AndKeyword;
		if (text == "or") return SyntaxKind.OrKeyword;
		if (text == "not") return SyntaxKind.NotKeyword;
		if (text == "with") return SyntaxKind.WithKeyword;
		if (text == "init") return SyntaxKind.InitKeyword;
		if (text == "record") return SyntaxKind.RecordKeyword;
		if (text == "managed") return SyntaxKind.ManagedKeyword;
		if (text == "unmanaged") return SyntaxKind.UnmanagedKeyword;
		if (text == "required") return SyntaxKind.RequiredKeyword;
		if (text == "scoped") return SyntaxKind.ScopedKeyword;
		if (text == "file") return SyntaxKind.FileKeyword;
		if (text == "allows") return SyntaxKind.AllowsKeyword;
		if (text == "extension") return SyntaxKind.ExtensionKeyword;
		return SyntaxKind.None;
	}

	public static string GetText(SyntaxKind kind)
	{
		if (kind == SyntaxKind.TildeToken) return "~";
		if (kind == SyntaxKind.ExclamationToken) return "!";
		if (kind == SyntaxKind.DollarToken) return "$";
		if (kind == SyntaxKind.PercentToken) return "%";
		if (kind == SyntaxKind.CaretToken) return "^";
		if (kind == SyntaxKind.AmpersandToken) return "&";
		if (kind == SyntaxKind.AsteriskToken) return "*";
		if (kind == SyntaxKind.OpenParenToken) return "(";
		if (kind == SyntaxKind.CloseParenToken) return ")";
		if (kind == SyntaxKind.MinusToken) return "-";
		if (kind == SyntaxKind.PlusToken) return "+";
		if (kind == SyntaxKind.EqualsToken) return "=";
		if (kind == SyntaxKind.OpenBraceToken) return "{";
		if (kind == SyntaxKind.CloseBraceToken) return "}";
		if (kind == SyntaxKind.OpenBracketToken) return "[";
		if (kind == SyntaxKind.CloseBracketToken) return "]";
		if (kind == SyntaxKind.BarToken) return "|";
		if (kind == SyntaxKind.BackslashToken) return "\\";
		if (kind == SyntaxKind.ColonToken) return ":";
		if (kind == SyntaxKind.SemicolonToken) return ";";
		if (kind == SyntaxKind.DoubleQuoteToken) return "\"";
		if (kind == SyntaxKind.SingleQuoteToken) return "'";
		if (kind == SyntaxKind.LessThanToken) return "<";
		if (kind == SyntaxKind.CommaToken) return ",";
		if (kind == SyntaxKind.GreaterThanToken) return ">";
		if (kind == SyntaxKind.DotToken) return ".";
		if (kind == SyntaxKind.QuestionToken) return "?";
		if (kind == SyntaxKind.HashToken) return "#";
		if (kind == SyntaxKind.SlashToken) return "/";
		if (kind == SyntaxKind.SlashGreaterThanToken) return "/>";
		if (kind == SyntaxKind.LessThanSlashToken) return "</";
		if (kind == SyntaxKind.XmlCommentStartToken) return "<!--";
		if (kind == SyntaxKind.XmlCommentEndToken) return "-->";
		if (kind == SyntaxKind.XmlCDataStartToken) return "<![CDATA[";
		if (kind == SyntaxKind.XmlCDataEndToken) return "]]>";
		if (kind == SyntaxKind.XmlProcessingInstructionStartToken) return "<?";
		if (kind == SyntaxKind.XmlProcessingInstructionEndToken) return "?>";
		if (kind == SyntaxKind.BarBarToken) return "||";
		if (kind == SyntaxKind.AmpersandAmpersandToken) return "&&";
		if (kind == SyntaxKind.MinusMinusToken) return "--";
		if (kind == SyntaxKind.PlusPlusToken) return "++";
		if (kind == SyntaxKind.ColonColonToken) return "::";
		if (kind == SyntaxKind.QuestionQuestionToken) return "??";
		if (kind == SyntaxKind.MinusGreaterThanToken) return "->";
		if (kind == SyntaxKind.ExclamationEqualsToken) return "!=";
		if (kind == SyntaxKind.EqualsEqualsToken) return "==";
		if (kind == SyntaxKind.EqualsGreaterThanToken) return "=>";
		if (kind == SyntaxKind.LessThanEqualsToken) return "<=";
		if (kind == SyntaxKind.LessThanLessThanToken) return "<<";
		if (kind == SyntaxKind.LessThanLessThanEqualsToken) return "<<=";
		if (kind == SyntaxKind.GreaterThanEqualsToken) return ">=";
		if (kind == SyntaxKind.GreaterThanGreaterThanToken) return ">>";
		if (kind == SyntaxKind.GreaterThanGreaterThanEqualsToken) return ">>=";
		if (kind == SyntaxKind.GreaterThanGreaterThanGreaterThanToken) return ">>>";
		if (kind == SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken) return ">>>=";
		if (kind == SyntaxKind.SlashEqualsToken) return "/=";
		if (kind == SyntaxKind.AsteriskEqualsToken) return "*=";
		if (kind == SyntaxKind.BarEqualsToken) return "|=";
		if (kind == SyntaxKind.AmpersandEqualsToken) return "&=";
		if (kind == SyntaxKind.PlusEqualsToken) return "+=";
		if (kind == SyntaxKind.MinusEqualsToken) return "-=";
		if (kind == SyntaxKind.CaretEqualsToken) return "^=";
		if (kind == SyntaxKind.PercentEqualsToken) return "%=";
		if (kind == SyntaxKind.QuestionQuestionEqualsToken) return "??=";
		if (kind == SyntaxKind.DotDotToken) return "..";
		if (kind == SyntaxKind.BoolKeyword) return "bool";
		if (kind == SyntaxKind.ByteKeyword) return "byte";
		if (kind == SyntaxKind.SByteKeyword) return "sbyte";
		if (kind == SyntaxKind.ShortKeyword) return "short";
		if (kind == SyntaxKind.UShortKeyword) return "ushort";
		if (kind == SyntaxKind.IntKeyword) return "int";
		if (kind == SyntaxKind.UIntKeyword) return "uint";
		if (kind == SyntaxKind.LongKeyword) return "long";
		if (kind == SyntaxKind.ULongKeyword) return "ulong";
		if (kind == SyntaxKind.DoubleKeyword) return "double";
		if (kind == SyntaxKind.FloatKeyword) return "float";
		if (kind == SyntaxKind.DecimalKeyword) return "decimal";
		if (kind == SyntaxKind.StringKeyword) return "string";
		if (kind == SyntaxKind.CharKeyword) return "char";
		if (kind == SyntaxKind.VoidKeyword) return "void";
		if (kind == SyntaxKind.ObjectKeyword) return "object";
		if (kind == SyntaxKind.TypeOfKeyword) return "typeof";
		if (kind == SyntaxKind.SizeOfKeyword) return "sizeof";
		if (kind == SyntaxKind.NullKeyword) return "null";
		if (kind == SyntaxKind.TrueKeyword) return "true";
		if (kind == SyntaxKind.FalseKeyword) return "false";
		if (kind == SyntaxKind.IfKeyword) return "if";
		if (kind == SyntaxKind.ElseKeyword) return "else";
		if (kind == SyntaxKind.WhileKeyword) return "while";
		if (kind == SyntaxKind.ForKeyword) return "for";
		if (kind == SyntaxKind.ForEachKeyword) return "foreach";
		if (kind == SyntaxKind.DoKeyword) return "do";
		if (kind == SyntaxKind.SwitchKeyword) return "switch";
		if (kind == SyntaxKind.CaseKeyword) return "case";
		if (kind == SyntaxKind.DefaultKeyword) return "default";
		if (kind == SyntaxKind.TryKeyword) return "try";
		if (kind == SyntaxKind.CatchKeyword) return "catch";
		if (kind == SyntaxKind.FinallyKeyword) return "finally";
		if (kind == SyntaxKind.LockKeyword) return "lock";
		if (kind == SyntaxKind.GotoKeyword) return "goto";
		if (kind == SyntaxKind.BreakKeyword) return "break";
		if (kind == SyntaxKind.ContinueKeyword) return "continue";
		if (kind == SyntaxKind.ReturnKeyword) return "return";
		if (kind == SyntaxKind.ThrowKeyword) return "throw";
		if (kind == SyntaxKind.PublicKeyword) return "public";
		if (kind == SyntaxKind.PrivateKeyword) return "private";
		if (kind == SyntaxKind.InternalKeyword) return "internal";
		if (kind == SyntaxKind.ProtectedKeyword) return "protected";
		if (kind == SyntaxKind.StaticKeyword) return "static";
		if (kind == SyntaxKind.ReadOnlyKeyword) return "readonly";
		if (kind == SyntaxKind.SealedKeyword) return "sealed";
		if (kind == SyntaxKind.ConstKeyword) return "const";
		if (kind == SyntaxKind.FixedKeyword) return "fixed";
		if (kind == SyntaxKind.StackAllocKeyword) return "stackalloc";
		if (kind == SyntaxKind.VolatileKeyword) return "volatile";
		if (kind == SyntaxKind.NewKeyword) return "new";
		if (kind == SyntaxKind.OverrideKeyword) return "override";
		if (kind == SyntaxKind.AbstractKeyword) return "abstract";
		if (kind == SyntaxKind.VirtualKeyword) return "virtual";
		if (kind == SyntaxKind.EventKeyword) return "event";
		if (kind == SyntaxKind.ExternKeyword) return "extern";
		if (kind == SyntaxKind.RefKeyword) return "ref";
		if (kind == SyntaxKind.OutKeyword) return "out";
		if (kind == SyntaxKind.InKeyword) return "in";
		if (kind == SyntaxKind.IsKeyword) return "is";
		if (kind == SyntaxKind.AsKeyword) return "as";
		if (kind == SyntaxKind.ParamsKeyword) return "params";
		if (kind == SyntaxKind.ArgListKeyword) return "__arglist";
		if (kind == SyntaxKind.MakeRefKeyword) return "__makeref";
		if (kind == SyntaxKind.RefTypeKeyword) return "__reftype";
		if (kind == SyntaxKind.RefValueKeyword) return "__refvalue";
		if (kind == SyntaxKind.ThisKeyword) return "this";
		if (kind == SyntaxKind.BaseKeyword) return "base";
		if (kind == SyntaxKind.NamespaceKeyword) return "namespace";
		if (kind == SyntaxKind.UsingKeyword) return "using";
		if (kind == SyntaxKind.ClassKeyword) return "class";
		if (kind == SyntaxKind.StructKeyword) return "struct";
		if (kind == SyntaxKind.InterfaceKeyword) return "interface";
		if (kind == SyntaxKind.EnumKeyword) return "enum";
		if (kind == SyntaxKind.DelegateKeyword) return "delegate";
		if (kind == SyntaxKind.CheckedKeyword) return "checked";
		if (kind == SyntaxKind.UncheckedKeyword) return "unchecked";
		if (kind == SyntaxKind.UnsafeKeyword) return "unsafe";
		if (kind == SyntaxKind.OperatorKeyword) return "operator";
		if (kind == SyntaxKind.ImplicitKeyword) return "implicit";
		if (kind == SyntaxKind.ExplicitKeyword) return "explicit";
		if (kind == SyntaxKind.ElifKeyword) return "elif";
		if (kind == SyntaxKind.EndIfKeyword) return "endif";
		if (kind == SyntaxKind.RegionKeyword) return "region";
		if (kind == SyntaxKind.EndRegionKeyword) return "endregion";
		if (kind == SyntaxKind.DefineKeyword) return "define";
		if (kind == SyntaxKind.UndefKeyword) return "undef";
		if (kind == SyntaxKind.WarningKeyword) return "warning";
		if (kind == SyntaxKind.ErrorKeyword) return "error";
		if (kind == SyntaxKind.LineKeyword) return "line";
		if (kind == SyntaxKind.PragmaKeyword) return "pragma";
		if (kind == SyntaxKind.HiddenKeyword) return "hidden";
		if (kind == SyntaxKind.ChecksumKeyword) return "checksum";
		if (kind == SyntaxKind.DisableKeyword) return "disable";
		if (kind == SyntaxKind.RestoreKeyword) return "restore";
		if (kind == SyntaxKind.ReferenceKeyword) return "r";
		if (kind == SyntaxKind.LoadKeyword) return "load";
		if (kind == SyntaxKind.NullableKeyword) return "nullable";
		if (kind == SyntaxKind.EnableKeyword) return "enable";
		if (kind == SyntaxKind.WarningsKeyword) return "warnings";
		if (kind == SyntaxKind.AnnotationsKeyword) return "annotations";
		if (kind == SyntaxKind.YieldKeyword) return "yield";
		if (kind == SyntaxKind.PartialKeyword) return "partial";
		if (kind == SyntaxKind.FromKeyword) return "from";
		if (kind == SyntaxKind.GroupKeyword) return "group";
		if (kind == SyntaxKind.JoinKeyword) return "join";
		if (kind == SyntaxKind.IntoKeyword) return "into";
		if (kind == SyntaxKind.LetKeyword) return "let";
		if (kind == SyntaxKind.ByKeyword) return "by";
		if (kind == SyntaxKind.WhereKeyword) return "where";
		if (kind == SyntaxKind.SelectKeyword) return "select";
		if (kind == SyntaxKind.GetKeyword) return "get";
		if (kind == SyntaxKind.SetKeyword) return "set";
		if (kind == SyntaxKind.AddKeyword) return "add";
		if (kind == SyntaxKind.RemoveKeyword) return "remove";
		if (kind == SyntaxKind.OrderByKeyword) return "orderby";
		if (kind == SyntaxKind.AliasKeyword) return "alias";
		if (kind == SyntaxKind.OnKeyword) return "on";
		if (kind == SyntaxKind.EqualsKeyword) return "equals";
		if (kind == SyntaxKind.AscendingKeyword) return "ascending";
		if (kind == SyntaxKind.DescendingKeyword) return "descending";
		if (kind == SyntaxKind.AssemblyKeyword) return "assembly";
		if (kind == SyntaxKind.ModuleKeyword) return "module";
		if (kind == SyntaxKind.TypeKeyword) return "type";
		if (kind == SyntaxKind.FieldKeyword) return "field";
		if (kind == SyntaxKind.MethodKeyword) return "method";
		if (kind == SyntaxKind.ParamKeyword) return "param";
		if (kind == SyntaxKind.PropertyKeyword) return "property";
		if (kind == SyntaxKind.TypeVarKeyword) return "typevar";
		if (kind == SyntaxKind.GlobalKeyword) return "global";
		if (kind == SyntaxKind.NameOfKeyword) return "nameof";
		if (kind == SyntaxKind.AsyncKeyword) return "async";
		if (kind == SyntaxKind.AwaitKeyword) return "await";
		if (kind == SyntaxKind.WhenKeyword) return "when";
		if (kind == SyntaxKind.InterpolatedStringStartToken) return "$\"";
		if (kind == SyntaxKind.InterpolatedStringEndToken) return "\"";
		if (kind == SyntaxKind.InterpolatedVerbatimStringStartToken) return "$@\"";
		if (kind == SyntaxKind.UnderscoreToken) return "_";
		if (kind == SyntaxKind.VarKeyword) return "var";
		if (kind == SyntaxKind.AndKeyword) return "and";
		if (kind == SyntaxKind.OrKeyword) return "or";
		if (kind == SyntaxKind.NotKeyword) return "not";
		if (kind == SyntaxKind.WithKeyword) return "with";
		if (kind == SyntaxKind.InitKeyword) return "init";
		if (kind == SyntaxKind.RecordKeyword) return "record";
		if (kind == SyntaxKind.ManagedKeyword) return "managed";
		if (kind == SyntaxKind.UnmanagedKeyword) return "unmanaged";
		if (kind == SyntaxKind.RequiredKeyword) return "required";
		if (kind == SyntaxKind.ScopedKeyword) return "scoped";
		if (kind == SyntaxKind.FileKeyword) return "file";
		if (kind == SyntaxKind.AllowsKeyword) return "allows";
		if (kind == SyntaxKind.ExtensionKeyword) return "extension";
		return string.Empty;
	}

	public static string GetText(Accessibility accessibility)
	{
		int val = (int)accessibility;
		if (val == 0) return string.Empty;
		if (val == 1) return GetText(SyntaxKind.PrivateKeyword);
		if (val == 2) return GetText(SyntaxKind.PrivateKeyword) + " " + GetText(SyntaxKind.ProtectedKeyword);
		if (val == 4) return GetText(SyntaxKind.InternalKeyword);
		if (val == 3) return GetText(SyntaxKind.ProtectedKeyword);
		if (val == 5) return GetText(SyntaxKind.ProtectedKeyword) + " " + GetText(SyntaxKind.InternalKeyword);
		if (val == 6) return GetText(SyntaxKind.PublicKeyword);
		throw new ArgumentOutOfRangeException(nameof(accessibility));
	}

	public static SyntaxKind GetOperatorKind(string operatorMetadataName)
	{
		if (operatorMetadataName == "op_CheckedAddition" || operatorMetadataName == "op_Addition") return SyntaxKind.PlusToken;
		if (operatorMetadataName == "op_BitwiseAnd") return SyntaxKind.AmpersandToken;
		if (operatorMetadataName == "op_BitwiseOr") return SyntaxKind.BarToken;
		if (operatorMetadataName == "op_Decrement" || operatorMetadataName == "op_CheckedDecrement" || operatorMetadataName == "op_CheckedDecrementAssignment" || operatorMetadataName == "op_DecrementAssignment") return SyntaxKind.MinusMinusToken;
		if (operatorMetadataName == "op_CheckedDivision" || operatorMetadataName == "op_Division") return SyntaxKind.SlashToken;
		if (operatorMetadataName == "op_Equality") return SyntaxKind.EqualsEqualsToken;
		if (operatorMetadataName == "op_ExclusiveOr") return SyntaxKind.CaretToken;
		if (operatorMetadataName == "op_CheckedExplicit" || operatorMetadataName == "op_Explicit") return SyntaxKind.ExplicitKeyword;
		if (operatorMetadataName == "op_False") return SyntaxKind.FalseKeyword;
		if (operatorMetadataName == "op_GreaterThan") return SyntaxKind.GreaterThanToken;
		if (operatorMetadataName == "op_GreaterThanOrEqual") return SyntaxKind.GreaterThanEqualsToken;
		if (operatorMetadataName == "op_Implicit") return SyntaxKind.ImplicitKeyword;
		if (operatorMetadataName == "op_Increment" || operatorMetadataName == "op_CheckedIncrement" || operatorMetadataName == "op_CheckedIncrementAssignment" || operatorMetadataName == "op_IncrementAssignment") return SyntaxKind.PlusPlusToken;
		if (operatorMetadataName == "op_Inequality") return SyntaxKind.ExclamationEqualsToken;
		if (operatorMetadataName == "op_LeftShift") return SyntaxKind.LessThanLessThanToken;
		if (operatorMetadataName == "op_LessThan") return SyntaxKind.LessThanToken;
		if (operatorMetadataName == "op_LessThanOrEqual") return SyntaxKind.LessThanEqualsToken;
		if (operatorMetadataName == "op_LogicalNot") return SyntaxKind.ExclamationToken;
		if (operatorMetadataName == "op_Modulus") return SyntaxKind.PercentToken;
		if (operatorMetadataName == "op_CheckedMultiply" || operatorMetadataName == "op_Multiply") return SyntaxKind.AsteriskToken;
		if (operatorMetadataName == "op_OnesComplement") return SyntaxKind.TildeToken;
		if (operatorMetadataName == "op_RightShift") return SyntaxKind.GreaterThanGreaterThanToken;
		if (operatorMetadataName == "op_UnsignedRightShift") return SyntaxKind.GreaterThanGreaterThanGreaterThanToken;
		if (operatorMetadataName == "op_Subtraction" || operatorMetadataName == "op_CheckedSubtraction") return SyntaxKind.MinusToken;
		if (operatorMetadataName == "op_True") return SyntaxKind.TrueKeyword;
		if (operatorMetadataName == "op_CheckedUnaryNegation" || operatorMetadataName == "op_UnaryNegation") return SyntaxKind.MinusToken;
		if (operatorMetadataName == "op_UnaryPlus") return SyntaxKind.PlusToken;
		if (operatorMetadataName == "op_AdditionAssignment" || operatorMetadataName == "op_CheckedAdditionAssignment") return SyntaxKind.PlusEqualsToken;
		if (operatorMetadataName == "op_DivisionAssignment" || operatorMetadataName == "op_CheckedDivisionAssignment") return SyntaxKind.SlashEqualsToken;
		if (operatorMetadataName == "op_CheckedMultiplicationAssignment" || operatorMetadataName == "op_MultiplicationAssignment") return SyntaxKind.AsteriskEqualsToken;
		if (operatorMetadataName == "op_CheckedSubtractionAssignment" || operatorMetadataName == "op_SubtractionAssignment") return SyntaxKind.MinusEqualsToken;
		if (operatorMetadataName == "op_ModulusAssignment") return SyntaxKind.PercentEqualsToken;
		if (operatorMetadataName == "op_BitwiseAndAssignment") return SyntaxKind.AmpersandEqualsToken;
		if (operatorMetadataName == "op_BitwiseOrAssignment") return SyntaxKind.BarEqualsToken;
		if (operatorMetadataName == "op_ExclusiveOrAssignment") return SyntaxKind.CaretEqualsToken;
		if (operatorMetadataName == "op_LeftShiftAssignment") return SyntaxKind.LessThanLessThanEqualsToken;
		if (operatorMetadataName == "op_RightShiftAssignment") return SyntaxKind.GreaterThanGreaterThanEqualsToken;
		if (operatorMetadataName == "op_UnsignedRightShiftAssignment") return SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken;
		return SyntaxKind.None;
	}

	public static bool IsCheckedOperator(string operatorMetadataName)
	{
		if (operatorMetadataName == "op_CheckedDecrement"
			|| operatorMetadataName == "op_CheckedIncrement"
			|| operatorMetadataName == "op_CheckedAddition"
			|| operatorMetadataName == "op_CheckedDivision"
			|| operatorMetadataName == "op_CheckedMultiply"
			|| operatorMetadataName == "op_CheckedExplicit"
			|| operatorMetadataName == "op_CheckedAdditionAssignment"
			|| operatorMetadataName == "op_CheckedDivisionAssignment"
			|| operatorMetadataName == "op_CheckedDecrementAssignment"
			|| operatorMetadataName == "op_CheckedIncrementAssignment"
			|| operatorMetadataName == "op_CheckedUnaryNegation"
			|| operatorMetadataName == "op_CheckedSubtraction"
			|| operatorMetadataName == "op_CheckedMultiplicationAssignment"
			|| operatorMetadataName == "op_CheckedSubtractionAssignment")
		{
			return true;
		}
		return false;
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
