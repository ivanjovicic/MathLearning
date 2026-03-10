using System.Text.RegularExpressions;
using MathLearning.Domain.Enums;

namespace MathLearning.Application.Content;

public interface IMathContentSanitizer
{
    string NormalizeMathContent(string? raw, ContentFormat format);
    IReadOnlyList<string> GetWarnings(string? raw, ContentFormat format);
    string? GenerateSemanticsAltText(string? raw, ContentFormat format);
}

public sealed class MathContentSanitizer : IMathContentSanitizer
{
    private static readonly Regex ScriptRegex = new(@"<\s*script\b[^>]*>.*?<\s*/\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlRegex = new(@"</?[a-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EventAttributeRegex = new(@"\son[a-z]+\s*=\s*(['""]).*?\1", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex UnsupportedLatexRegex = new(@"\\(htmlClass|href|includegraphics|write|input|catcode|openout|read)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string NormalizeMathContent(string? raw, ContentFormat format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim();
        normalized = normalized.Replace("\r\n", "\n", StringComparison.Ordinal)
                               .Replace('\r', '\n');
        normalized = ScriptRegex.Replace(normalized, string.Empty);
        normalized = EventAttributeRegex.Replace(normalized, string.Empty);

        if (format != ContentFormat.Html)
        {
            normalized = HtmlRegex.Replace(normalized, string.Empty);
        }

        normalized = NormalizeDelimiters(normalized);
        normalized = normalized.Replace("−", "-", StringComparison.Ordinal)
                               .Replace("×", "\\times", StringComparison.Ordinal)
                               .Replace("÷", "\\div", StringComparison.Ordinal);
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    public IReadOnlyList<string> GetWarnings(string? raw, ContentFormat format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>();
        if (HasUnbalancedBraces(raw))
        {
            warnings.Add("Unbalanced braces detected.");
        }

        if (HasUnbalancedDollarDelimiters(raw))
        {
            warnings.Add("Unbalanced inline math delimiters detected.");
        }

        if (UnsupportedLatexRegex.IsMatch(raw))
        {
            warnings.Add("Unsupported LaTeX commands detected.");
        }

        if (format == ContentFormat.Html && HtmlRegex.IsMatch(raw))
        {
            warnings.Add("HTML content will be sanitized before rendering.");
        }

        if (raw.Contains(@"\frac", StringComparison.Ordinal) && !raw.Contains('{'))
        {
            warnings.Add(@"Malformed \frac expression detected.");
        }

        if (raw.Contains(@"\sqrt", StringComparison.Ordinal) && !raw.Contains('{'))
        {
            warnings.Add(@"Malformed \sqrt expression detected.");
        }

        return warnings;
    }

    public string? GenerateSemanticsAltText(string? raw, ContentFormat format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = NormalizeMathContent(raw, format);
        var readable = normalized.Replace(@"\frac", " fraction ", StringComparison.Ordinal)
                                 .Replace(@"\sqrt", " square root ", StringComparison.Ordinal)
                                 .Replace("^2", " squared", StringComparison.Ordinal)
                                 .Replace("^3", " cubed", StringComparison.Ordinal)
                                 .Replace("_", " sub ", StringComparison.Ordinal)
                                 .Replace(@"\times", " times ", StringComparison.Ordinal)
                                 .Replace(@"\div", " divided by ", StringComparison.Ordinal)
                                 .Replace("$", string.Empty, StringComparison.Ordinal)
                                 .Replace("{", " ", StringComparison.Ordinal)
                                 .Replace("}", " ", StringComparison.Ordinal);
        readable = MultiWhitespaceRegex.Replace(readable, " ").Trim();
        return string.IsNullOrWhiteSpace(readable) ? null : readable;
    }

    private static string NormalizeDelimiters(string value)
        => value.Replace(@"\(", "$", StringComparison.Ordinal)
                .Replace(@"\)", "$", StringComparison.Ordinal)
                .Replace(@"\[", "$$", StringComparison.Ordinal)
                .Replace(@"\]", "$$", StringComparison.Ordinal);

    private static bool HasUnbalancedBraces(string value)
    {
        var depth = 0;
        foreach (var c in value)
        {
            if (c == '{') depth++;
            if (c == '}') depth--;
            if (depth < 0) return true;
        }

        return depth != 0;
    }

    private static bool HasUnbalancedDollarDelimiters(string value)
    {
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\')
            {
                i++;
                continue;
            }

            if (value[i] == '$')
            {
                count++;
            }
        }

        return count % 2 != 0;
    }
}
