using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace FancyEnumGenerator.Internal;

internal static class GeneratorTextExtensions
{
    private const string CSharpStringLiteralEscapeCharacters = "\\\"\r\n\t";

    public static string ToCSharpStringLiteral(this string value)
    {
        if (value.AsSpan().IndexOfAny(CSharpStringLiteralEscapeCharacters.AsSpan()) < 0)
        {
            return string.Concat("\"", value, "\"");
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.Append('"').ToString();
    }

    public static string ToSafeHintName(this string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sb.ToString();
    }

    public static string ToSafeCSharpIdentifier(this string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "_";
        }

        var rawSpan = raw.AsSpan();
        if ((char.IsLetter(rawSpan[0]) || rawSpan[0] == '_') && IsSafeCSharpIdentifierTail(rawSpan.Slice(1)))
        {
            return raw;
        }

        var sb = new StringBuilder(raw.Length + (char.IsDigit(rawSpan[0]) ? 1 : 0));
        for (var index = 0; index < raw.Length; index++)
        {
            var ch = raw[index];
            if (index == 0)
            {
                if (char.IsLetter(ch) || ch == '_')
                {
                    sb.Append(ch);
                    continue;
                }

                sb.Append('_');
                if (!char.IsDigit(ch))
                {
                    continue;
                }
            }

            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                sb.Append('_');
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    public static string ToPascalCaseCSharpIdentifier(this string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "_";
        }

        var rawSpan = raw.AsSpan();
        var needsTransform = !char.IsUpper(rawSpan[0]);
        for (var index = 1; !needsTransform && index < rawSpan.Length; index++)
        {
            needsTransform = !char.IsLetterOrDigit(rawSpan[index]);
        }
        if (!needsTransform)
        {
            return raw;
        }

        var sb = new StringBuilder(raw.Length + (char.IsDigit(raw[0]) ? 1 : 0));
        var capitalizeNext = true;
        foreach (var ch in raw)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                capitalizeNext = true;
                continue;
            }

            if (sb.Length == 0 && char.IsDigit(ch))
            {
                sb.Append('_');
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
            capitalizeNext = false;
        }

        return sb.Length == 0 ? "_" : sb.ToString();
    }

    private static bool IsSafeCSharpIdentifierTail(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                return false;
            }
        }

        return true;
    }

    public static string EscapeCSharpIdentifier(this string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "_";
        }

        if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None)
        {
            return $"@{identifier}";
        }

        return identifier;
    }
}
