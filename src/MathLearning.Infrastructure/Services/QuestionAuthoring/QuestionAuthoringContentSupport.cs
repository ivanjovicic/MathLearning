using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.Questions;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

internal enum AuthoringSegmentKind
{
    Text,
    InlineMath,
    DisplayMath
}

internal sealed record AuthoringContentSegment(AuthoringSegmentKind Kind, string Value)
{
    public string KindName => Kind switch
    {
        AuthoringSegmentKind.InlineMath => "inline_math",
        AuthoringSegmentKind.DisplayMath => "display_math",
        _ => "text"
    };
}

internal sealed record LatexSegmentValidation(
    bool IsValid,
    string? NormalizedExpression,
    string? ErrorCode,
    string? ErrorMessage,
    string? SafeFallbackText);

internal static class QuestionAuthoringContentSupport
{
    private static readonly Regex HtmlTagRegex = new(@"</?[a-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SuspiciousWhitespaceRegex = new(@"\n\s*\n\s*\n", RegexOptions.Compiled);
    private static readonly Regex LatexCommandRegex = new(@"\\([A-Za-z]+)", RegexOptions.Compiled);
    private static readonly Regex ExponentRegex = new(@"(?<!\\)\^([A-Za-z0-9])", RegexOptions.Compiled);
    private static readonly Regex SubscriptRegex = new(@"(?<!\\)_([A-Za-z0-9])", RegexOptions.Compiled);
    private static readonly Regex BareSqrtRegex = new(@"(?<!\\)\bsqrt\(([^()]+)\)", RegexOptions.Compiled);
    private static readonly Regex UnsafeScriptRegex = new(@"<\s*script\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> SupportedCommands =
    [
        "frac", "sqrt", "left", "right", "cdot", "times", "div", "pm", "mp",
        "leq", "geq", "neq", "approx", "alpha", "beta", "gamma", "delta",
        "theta", "pi", "sin", "cos", "tan", "log", "ln", "sum", "prod",
        "int", "infty", "overline", "underline", "text", "mathrm", "mathbf",
        "begin", "end", "cases", "aligned", "matrix", "pmatrix", "bmatrix"
    ];

    private static readonly HashSet<string> SupportedEnvironments =
    [
        "cases", "aligned", "matrix", "pmatrix", "bmatrix"
    ];

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    public static string ComputeContentHash<T>(T payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public static bool ContainsUnsafeMarkup(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (UnsafeScriptRegex.IsMatch(value) || HtmlTagRegex.IsMatch(value));

    public static bool HasSuspiciousFormatting(string? value)
        => !string.IsNullOrWhiteSpace(value) && SuspiciousWhitespaceRegex.IsMatch(value);

    public static string NormalizeVisibleText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace("\r", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Replace("$", string.Empty)
                               .Replace(@"\(", string.Empty)
                               .Replace(@"\)", string.Empty)
                               .Replace(@"\[", string.Empty)
                               .Replace(@"\]", string.Empty);
        return normalized.Trim();
    }

    public static IReadOnlyList<string> GetUnsupportedCommands(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return LatexCommandRegex.Matches(value)
            .Select(m => m.Groups[1].Value)
            .Where(command => !SupportedCommands.Contains(command))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool HasBalancedBraces(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\')
            {
                i++;
                continue;
            }

            if (value[i] == '{')
            {
                depth++;
                continue;
            }

            if (value[i] == '}')
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }

    public static bool TrySegment(string? value, out List<AuthoringContentSegment> segments, out string? errorCode, out string? errorMessage)
    {
        var localSegments = new List<AuthoringContentSegment>();
        segments = localSegments;
        errorCode = null;
        errorMessage = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        var textBuffer = new StringBuilder();

        void FlushText()
        {
            if (textBuffer.Length > 0)
            {
                localSegments.Add(new AuthoringContentSegment(AuthoringSegmentKind.Text, textBuffer.ToString()));
                textBuffer.Clear();
            }
        }

        for (var index = 0; index < value.Length;)
        {
            if (MatchesAt(value, index, "$$"))
            {
                FlushText();
                var closing = value.IndexOf("$$", index + 2, StringComparison.Ordinal);
                if (closing < 0)
                {
                    errorCode = "latex.unbalanced_display_delimiter";
                    errorMessage = "Display math delimiter $$ is not closed.";
                    return false;
                }

                localSegments.Add(new AuthoringContentSegment(
                    AuthoringSegmentKind.DisplayMath,
                    value[(index + 2)..closing]));
                index = closing + 2;
                continue;
            }

            if (MatchesAt(value, index, @"\["))
            {
                FlushText();
                var closing = value.IndexOf(@"\]", index + 2, StringComparison.Ordinal);
                if (closing < 0)
                {
                    errorCode = "latex.unbalanced_display_delimiter";
                    errorMessage = @"Display math delimiter \[ is not closed.";
                    return false;
                }

                localSegments.Add(new AuthoringContentSegment(
                    AuthoringSegmentKind.DisplayMath,
                    value[(index + 2)..closing]));
                index = closing + 2;
                continue;
            }

            if (MatchesAt(value, index, @"\("))
            {
                FlushText();
                var closing = value.IndexOf(@"\)", index + 2, StringComparison.Ordinal);
                if (closing < 0)
                {
                    errorCode = "latex.unbalanced_inline_delimiter";
                    errorMessage = @"Inline math delimiter \( is not closed.";
                    return false;
                }

                localSegments.Add(new AuthoringContentSegment(
                    AuthoringSegmentKind.InlineMath,
                    value[(index + 2)..closing]));
                index = closing + 2;
                continue;
            }

            if (value[index] == '$')
            {
                FlushText();
                var closing = FindClosingInlineDollar(value, index + 1);
                if (closing < 0)
                {
                    errorCode = "latex.unbalanced_inline_delimiter";
                    errorMessage = "Inline math delimiter $ is not closed.";
                    return false;
                }

                localSegments.Add(new AuthoringContentSegment(
                    AuthoringSegmentKind.InlineMath,
                    value[(index + 1)..closing]));
                index = closing + 1;
                continue;
            }

            textBuffer.Append(value[index]);
            index++;
        }

        FlushText();
        return true;
    }

    public static string NormalizeMixedContent(string? value, out IReadOnlyList<ContentSegmentDto> segments)
    {
        segments = Array.Empty<ContentSegmentDto>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!TrySegment(value, out var contentSegments, out _, out _))
        {
            var fallback = NormalizeText(value);
            segments = [new ContentSegmentDto("text", fallback)];
            return fallback;
        }

        var normalizedSegments = new List<ContentSegmentDto>(contentSegments.Count);
        var builder = new StringBuilder();

        foreach (var segment in contentSegments)
        {
            if (segment.Kind == AuthoringSegmentKind.Text)
            {
                var text = NormalizeText(segment.Value);
                normalizedSegments.Add(new ContentSegmentDto("text", text));
                builder.Append(text);
                continue;
            }

            var math = NormalizeMathExpression(segment.Value);
            normalizedSegments.Add(new ContentSegmentDto(segment.KindName, math));
            builder.Append(segment.Kind == AuthoringSegmentKind.DisplayMath ? "$$" : "$");
            builder.Append(math);
            builder.Append(segment.Kind == AuthoringSegmentKind.DisplayMath ? "$$" : "$");
        }

        segments = normalizedSegments;
        return builder.ToString().Trim();
    }

    public static string NormalizeMathExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var normalized = expression.Trim();
        normalized = normalized.Replace(@"\(", string.Empty)
                               .Replace(@"\)", string.Empty)
                               .Replace(@"\[", string.Empty)
                               .Replace(@"\]", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = BareSqrtRegex.Replace(normalized, @"\sqrt{$1}");
        normalized = ExponentRegex.Replace(normalized, "^{$1}");
        normalized = SubscriptRegex.Replace(normalized, "_{$1}");
        normalized = Regex.Replace(normalized, @"\s*([=+\-*/])\s*", "$1");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }

    public static LatexSegmentValidation ValidateMathSegment(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new LatexSegmentValidation(true, string.Empty, null, null, null);
        }

        if (expression.Length > 2048)
        {
            return Invalid("latex.expression_too_large", "Math expression exceeds the supported size limit.", expression);
        }

        if (!HasBalancedBraces(expression))
        {
            return Invalid("latex.unbalanced_braces", "Math expression has unbalanced braces.", expression);
        }

        var unsupportedCommands = GetUnsupportedCommands(expression);
        if (unsupportedCommands.Count > 0)
        {
            return Invalid(
                "latex.unsupported_command",
                $"Unsupported LaTeX command(s): {string.Join(", ", unsupportedCommands)}.",
                expression);
        }

        var environmentResult = ValidateEnvironments(expression);
        if (environmentResult is not null)
        {
            return environmentResult;
        }

        if (HasBrokenScriptUsage(expression))
        {
            return Invalid("latex.invalid_script", "Superscript or subscript usage is incomplete.", expression);
        }

        if (HasMalformedFrac(expression))
        {
            return Invalid("latex.invalid_fraction", @"\frac requires two braced arguments.", expression);
        }

        if (HasMalformedSqrt(expression))
        {
            return Invalid("latex.invalid_sqrt", @"\sqrt requires a braced argument.", expression);
        }

        if (GetMaxBraceDepth(expression) > 16)
        {
            return Invalid("latex.nesting_too_deep", "Math expression nesting depth exceeds the supported limit.", expression);
        }

        var normalized = NormalizeMathExpression(expression);
        return new LatexSegmentValidation(true, normalized, null, null, null);
    }

    public static string BuildSafeFallbackText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = Regex.Replace(value, @"\\([A-Za-z]+)", string.Empty);
        sanitized = sanitized.Replace("{", string.Empty)
                             .Replace("}", string.Empty)
                             .Replace("$", string.Empty)
                             .Replace(@"\(", string.Empty)
                             .Replace(@"\)", string.Empty)
                             .Replace(@"\[", string.Empty)
                             .Replace(@"\]", string.Empty);
        return NormalizeText(sanitized);
    }

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
                              .Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    public static string ConvertForEvaluation(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var normalized = NormalizeMathExpression(expression)
            .Replace(@"\cdot", "*", StringComparison.Ordinal)
            .Replace(@"\times", "*", StringComparison.Ordinal)
            .Replace(@"\div", "/", StringComparison.Ordinal)
            .Replace(@"\left", string.Empty, StringComparison.Ordinal)
            .Replace(@"\right", string.Empty, StringComparison.Ordinal);

        normalized = ExpandLatexFractions(normalized);
        normalized = ExpandLatexSquareRoots(normalized);
        normalized = normalized.Replace("{", "(", StringComparison.Ordinal)
                               .Replace("}", ")", StringComparison.Ordinal)
                               .Replace("−", "-", StringComparison.Ordinal)
                               .Replace("–", "-", StringComparison.Ordinal)
                               .Replace("×", "*", StringComparison.Ordinal)
                               .Replace("÷", "/", StringComparison.Ordinal)
                               .Replace(",", ".", StringComparison.Ordinal);
        return InsertImplicitMultiplication(normalized);
    }

    public static HashSet<string> ExtractIdentifiers(string expression)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(expression, @"[A-Za-z]+"))
        {
            var value = match.Value;
            if (value is "sqrt" or "sin" or "cos" or "tan" or "log" or "ln")
            {
                continue;
            }

            identifiers.Add(value);
        }

        return identifiers;
    }

    private static LatexSegmentValidation? ValidateEnvironments(string expression)
    {
        var stack = new Stack<string>();
        foreach (Match beginMatch in Regex.Matches(expression, @"\\(begin|end)\{([^}]+)\}"))
        {
            var command = beginMatch.Groups[1].Value;
            var environment = beginMatch.Groups[2].Value;
            if (!SupportedEnvironments.Contains(environment))
            {
                return Invalid(
                    "latex.unsupported_environment",
                    $"Unsupported LaTeX environment '{environment}'.",
                    expression);
            }

            if (string.Equals(command, "begin", StringComparison.Ordinal))
            {
                stack.Push(environment);
                continue;
            }

            if (stack.Count == 0 || !string.Equals(stack.Pop(), environment, StringComparison.Ordinal))
            {
                return Invalid(
                    "latex.environment_mismatch",
                    $"LaTeX environment '{environment}' is not properly closed.",
                    expression);
            }
        }

        if (stack.Count > 0)
        {
            return Invalid("latex.environment_mismatch", "LaTeX environment is not properly closed.", expression);
        }

        return null;
    }

    private static bool HasBrokenScriptUsage(string expression)
    {
        for (var index = 0; index < expression.Length; index++)
        {
            if (expression[index] is not ('^' or '_'))
            {
                continue;
            }

            if (index == expression.Length - 1)
            {
                return true;
            }

            var next = expression[index + 1];
            if (next == '{')
            {
                continue;
            }

            if (char.IsWhiteSpace(next))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMalformedFrac(string expression)
    {
        var index = 0;
        while ((index = expression.IndexOf(@"\frac", index, StringComparison.Ordinal)) >= 0)
        {
            var cursor = index + 5;
            if (!TryReadBracedBlock(expression, ref cursor, out _) ||
                !TryReadBracedBlock(expression, ref cursor, out _))
            {
                return true;
            }

            index = cursor;
        }

        return false;
    }

    private static bool HasMalformedSqrt(string expression)
    {
        var index = 0;
        while ((index = expression.IndexOf(@"\sqrt", index, StringComparison.Ordinal)) >= 0)
        {
            var cursor = index + 5;
            if (cursor < expression.Length && expression[cursor] == '[')
            {
                if (!TryReadBracketBlock(expression, ref cursor, out _))
                {
                    return true;
                }
            }

            if (!TryReadBracedBlock(expression, ref cursor, out _))
            {
                return true;
            }

            index = cursor;
        }

        return false;
    }

    private static int GetMaxBraceDepth(string expression)
    {
        var current = 0;
        var max = 0;
        foreach (var character in expression)
        {
            if (character == '{')
            {
                current++;
                max = Math.Max(max, current);
            }
            else if (character == '}')
            {
                current = Math.Max(0, current - 1);
            }
        }

        return max;
    }

    private static LatexSegmentValidation Invalid(string code, string message, string value)
        => new(false, null, code, message, BuildSafeFallbackText(value));

    private static bool MatchesAt(string source, int index, string token)
        => index + token.Length <= source.Length &&
           string.CompareOrdinal(source, index, token, 0, token.Length) == 0;

    private static int FindClosingInlineDollar(string source, int startIndex)
    {
        for (var index = startIndex; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
                continue;
            }

            if (source[index] == '$')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryReadBracedBlock(string source, ref int cursor, out string content)
    {
        content = string.Empty;
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
        {
            cursor++;
        }

        if (cursor >= source.Length || source[cursor] != '{')
        {
            return false;
        }

        cursor++;
        var start = cursor;
        var depth = 1;
        while (cursor < source.Length)
        {
            if (source[cursor] == '{')
            {
                depth++;
            }
            else if (source[cursor] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    content = source[start..cursor];
                    cursor++;
                    return true;
                }
            }

            cursor++;
        }

        return false;
    }

    private static bool TryReadBracketBlock(string source, ref int cursor, out string content)
    {
        content = string.Empty;
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
        {
            cursor++;
        }

        if (cursor >= source.Length || source[cursor] != '[')
        {
            return false;
        }

        cursor++;
        var start = cursor;
        while (cursor < source.Length && source[cursor] != ']')
        {
            cursor++;
        }

        if (cursor >= source.Length)
        {
            return false;
        }

        content = source[start..cursor];
        cursor++;
        return true;
    }

    private static string ExpandLatexFractions(string source)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < source.Length; index++)
        {
            if (!MatchesAt(source, index, @"\frac"))
            {
                builder.Append(source[index]);
                continue;
            }

            var cursor = index + 5;
            if (!TryReadBracedBlock(source, ref cursor, out var numerator) ||
                !TryReadBracedBlock(source, ref cursor, out var denominator))
            {
                builder.Append(source[index]);
                continue;
            }

            builder.Append("((");
            builder.Append(ExpandLatexFractions(numerator));
            builder.Append(")/((");
            builder.Append(ExpandLatexFractions(denominator));
            builder.Append("))");
            index = cursor - 1;
        }

        return builder.ToString();
    }

    private static string ExpandLatexSquareRoots(string source)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < source.Length; index++)
        {
            if (!MatchesAt(source, index, @"\sqrt"))
            {
                builder.Append(source[index]);
                continue;
            }

            var cursor = index + 5;
            if (cursor < source.Length && source[cursor] == '[')
            {
                if (!TryReadBracketBlock(source, ref cursor, out _))
                {
                    builder.Append(source[index]);
                    continue;
                }
            }

            if (!TryReadBracedBlock(source, ref cursor, out var radicand))
            {
                builder.Append(source[index]);
                continue;
            }

            builder.Append("sqrt(");
            builder.Append(ExpandLatexSquareRoots(radicand));
            builder.Append(')');
            index = cursor - 1;
        }

        return builder.ToString();
    }

    private static string InsertImplicitMultiplication(string source)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            builder.Append(current);

            if (index == source.Length - 1)
            {
                continue;
            }

            var next = source[index + 1];
            if (ShouldInsertMultiplication(current, next))
            {
                builder.Append('*');
            }
        }

        return builder.ToString();
    }

    private static bool ShouldInsertMultiplication(char current, char next)
    {
        if (char.IsWhiteSpace(current) || char.IsWhiteSpace(next))
        {
            return false;
        }

        if (current == '.' || next == '.')
        {
            return false;
        }

        if (current is '+' or '-' or '*' or '/' or '^' or '(' or '=')
        {
            return false;
        }

        if (next is '+' or '-' or '*' or '/' or '^' or ')' or '=')
        {
            return false;
        }

        var currentOperand = char.IsDigit(current) || char.IsLetter(current) || current == ')';
        var nextOperand = char.IsDigit(next) || char.IsLetter(next) || next == '(';
        return currentOperand && nextOperand;
    }
}
