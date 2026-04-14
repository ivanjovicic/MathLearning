using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using MathLearning.Domain.Explanations;

namespace MathLearning.Api.Services;

internal static partial class ExplanationEngineSupport
{
    public static bool IsSerbian(string language) =>
        !string.IsNullOrWhiteSpace(language) &&
        language.StartsWith("sr", StringComparison.OrdinalIgnoreCase);

    public static DifficultyLevel ParseDifficulty(string raw)
    {
        if (Enum.TryParse<DifficultyLevel>(raw, true, out var parsed))
            return parsed;

        return DifficultyLevel.Medium;
    }

    public static string ToContractString(Enum value) =>
        ContractCasePattern().Replace(value.ToString(), "_$1").ToUpperInvariant();

    public static string ComputeHash(params string?[] values)
    {
        var material = string.Join("||", values.Select(v => v?.Trim() ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ToLatex(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return expression;

        var output = FractionPattern().Replace(expression, "\\\\frac{$1}{$2}");
        output = output.Replace("*", " \\times ", StringComparison.Ordinal);
        return Regex.Replace(output, @"\s+", " ").Trim();
    }

    public static string ToMathMl(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "<math></math>";

        var encoded = HtmlEncoder.Default.Encode(expression.Trim());
        var replaced = FractionPattern().Replace(encoded, "<mfrac><mn>$1</mn><mn>$2</mn></mfrac>");
        return $"<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow>{replaced}</mrow></math>";
    }

    public static string FormatNumber(decimal value)
    {
        if (decimal.Truncate(value) == value)
            return decimal.ToInt32(value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"(-?\d+)\s*/\s*(\d+)")]
    private static partial Regex FractionPattern();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex ContractCasePattern();
}
