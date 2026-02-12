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

        // Keep already-wrapped inline math intact and process only plain segments.
        var parts = ExistingInlineMathRegex().Split(input);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length >= 2 && part.StartsWith('$') && part.EndsWith('$'))
                continue;

            part = CompositionCallRegex().Replace(part, m => WrapInline(m.Value));
            part = FunctionEqualityRegex().Replace(part, m => WrapInline(m.Value));
            parts[i] = part;
        }

        return string.Concat(parts);
    }

    private static string WrapInline(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('$') && trimmed.EndsWith('$'))
            return trimmed;

        return $"${trimmed}$";
    }
}
