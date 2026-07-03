using System.Text;
using System.Text.RegularExpressions;

namespace MathLearning.Application.Helpers;

/// <summary>
/// Normalizes mixed natural language + math text so common math fragments are
/// consistently wrapped as inline LaTeX: $...$.
/// </summary>
public static partial class InlineLatexFormatter
{
    [GeneratedRegex(@"\$[^$]+\$", RegexOptions.Compiled)]
    private static partial Regex ExistingInlineMathRegex();

    [GeneratedRegex(@"\(\s*[a-zA-Z]\s*(?:\\circ|\u2218)\s*[a-zA-Z]\s*\)\s*\(\s*[^()]+\s*\)", RegexOptions.Compiled)]
    private static partial Regex CompositionCallRegex();

    // Matches fragments like: f(x)=2x+3, g(x)=x^2, g(2)=4 ...
    [GeneratedRegex(@"[a-zA-Z]\s*\(\s*[^()]+\s*\)\s*=\s*[^,;:.]+?(?=(?:\s+i\s+|\s+and\s+|,|;|\.|$))", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FunctionEqualityRegex();

    public static string? NormalizeMixedInlineMath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var existingInlineMath = ExistingInlineMathRegex();
        var matches = existingInlineMath.Matches(input);
        if (matches.Count == 0)
            return NormalizePlainSegment(input);

        var result = new StringBuilder(input.Length + 16);
        var currentIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > currentIndex)
            {
                result.Append(NormalizePlainSegment(input[currentIndex..match.Index]));
            }

            // Existing inline math is already authoritative formatting. Keep it byte-for-byte.
            result.Append(match.Value);
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < input.Length)
        {
            result.Append(NormalizePlainSegment(input[currentIndex..]));
        }

        return result.ToString();
    }

    private static string NormalizePlainSegment(string value)
    {
        var normalized = CompositionCallRegex().Replace(value, match => WrapInline(match.Value));
        return FunctionEqualityRegex().Replace(normalized, match => WrapInline(match.Value));
    }

    private static string WrapInline(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('$') && trimmed.EndsWith('$'))
            return trimmed;

        return $"${trimmed}$";
    }
}
