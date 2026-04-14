using System.Globalization;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Explanations;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed partial class CommonMistakeDetector : ICommonMistakeDetector
{
    private readonly ApiDbContext _db;

    public CommonMistakeDetector(ApiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MistakeInsightDto>> DetectAsync(MathProblemDescriptor descriptor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descriptor.StudentAnswer))
            return Array.Empty<MistakeInsightDto>();

        var results = new List<MistakeInsightDto>(2);
        var problem = descriptor.ProblemText.Trim();
        var student = NormalizeAnswer(descriptor.StudentAnswer);
        var expected = NormalizeAnswer(descriptor.ExpectedAnswer);

        if (TryDetectFractionDenominatorAddition(problem, student, out var fractionMistake))
            results.Add(await BuildInsightAsync(descriptor, CommonMistakeType.FractionDenominatorAddition, fractionMistake.description, fractionMistake.remediation, 0.96m, "fraction_addition_rule", ct));

        if (TryDetectLinearSignError(problem, student, out var signMistake))
            results.Add(await BuildInsightAsync(descriptor, CommonMistakeType.SignError, signMistake.description, signMistake.remediation, 0.92m, "linear_equation_isolation", ct));

        if (TryDetectQuadraticFormulaUsage(problem, student, out var formulaMistake))
            results.Add(await BuildInsightAsync(descriptor, CommonMistakeType.IncorrectFormulaUsage, formulaMistake.description, formulaMistake.remediation, 0.88m, "quadratic_formula", ct));

        if (results.Count == 0 &&
            TryParseNumericAnswer(student, out var studentNumeric) &&
            TryParseNumericAnswer(expected, out var expectedNumeric) &&
            studentNumeric != expectedNumeric)
        {
            results.Add(await BuildInsightAsync(
                descriptor,
                CommonMistakeType.ArithmeticSlip,
                "The overall method looks close, but the arithmetic result is off.",
                "Re-check the final calculation one line at a time.",
                0.65m,
                null,
                ct));
        }

        return results;
    }

    private async Task<MistakeInsightDto> BuildInsightAsync(
        MathProblemDescriptor descriptor,
        CommonMistakeType mistakeType,
        string defaultDescription,
        string defaultRemediation,
        decimal confidence,
        string? formulaReferenceId,
        CancellationToken ct)
    {
        var overridePattern = await _db.CommonMistakePatterns
            .AsNoTracking()
            .Where(x =>
                x.MistakeType == mistakeType.ToString() &&
                x.Topic == descriptor.Context.Topic &&
                (x.Subtopic == null || x.Subtopic == descriptor.Context.Subtopic))
            .OrderBy(x => x.Priority)
            .FirstOrDefaultAsync(ct);

        return new MistakeInsightDto(
            ExplanationEngineSupport.ToContractString(mistakeType),
            overridePattern?.Description ?? defaultDescription,
            overridePattern?.Remediation ?? defaultRemediation,
            confidence,
            formulaReferenceId);
    }

    private static bool TryDetectFractionDenominatorAddition(string problem, string studentAnswer, out (string description, string remediation) result)
    {
        var match = FractionOperationPattern().Match(problem);
        if (!match.Success || !TryParseFraction(studentAnswer, out var studentFraction))
        {
            result = default;
            return false;
        }

        var ln = int.Parse(match.Groups["ln"].Value, CultureInfo.InvariantCulture);
        var ld = int.Parse(match.Groups["ld"].Value, CultureInfo.InvariantCulture);
        var rn = int.Parse(match.Groups["rn"].Value, CultureInfo.InvariantCulture);
        var rd = int.Parse(match.Groups["rd"].Value, CultureInfo.InvariantCulture);

        var wrongNumerator = ln + rn;
        var wrongDenominator = ld + rd;
        var normalizedWrong = NormalizeFraction(wrongNumerator, wrongDenominator);

        if (studentFraction == normalizedWrong)
        {
            result = (
                "You added the denominators together. When adding fractions, denominators stay fixed once they match.",
                "Find a common denominator first, then add only the numerators.");
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryDetectLinearSignError(string problem, string studentAnswer, out (string description, string remediation) result)
    {
        var match = LinearEquationPattern().Match(problem);
        if (!match.Success || !TryParseNumericAnswer(studentAnswer, out var studentValue))
        {
            result = default;
            return false;
        }

        var coefficientGroup = match.Groups["coef"].Value.Replace(" ", string.Empty, StringComparison.Ordinal);
        var offsetGroup = match.Groups["offset"].Value.Replace(" ", string.Empty, StringComparison.Ordinal);
        var rhs = int.Parse(match.Groups["rhs"].Value, CultureInfo.InvariantCulture);
        var coefficient = coefficientGroup switch
        {
            "" or "+" => 1,
            "-" => -1,
            _ => int.Parse(coefficientGroup, CultureInfo.InvariantCulture)
        };

        var offset = string.IsNullOrWhiteSpace(offsetGroup)
            ? 0
            : int.Parse(offsetGroup, CultureInfo.InvariantCulture);

        if (coefficient == 0 || offset == 0)
        {
            result = default;
            return false;
        }

        var wrong = (decimal)(rhs + offset) / coefficient;
        if (studentValue == wrong)
        {
            result = (
                "The sign changed in the wrong direction when you moved the constant term.",
                "Move the constant by applying the opposite operation on both sides.");
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryDetectQuadraticFormulaUsage(string problem, string studentAnswer, out (string description, string remediation) result)
    {
        var match = QuadraticPattern().Match(problem);
        if (!match.Success)
        {
            result = default;
            return false;
        }

        var normalized = studentAnswer.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (normalized.Contains("/a", StringComparison.Ordinal) || normalized.Contains("sqrt(b^2-4ac)/a", StringComparison.Ordinal))
        {
            result = (
                "The quadratic formula denominator should be 2a, not a.",
                "Write the full formula first, then substitute values carefully.");
            return true;
        }

        result = default;
        return false;
    }

    private static string NormalizeAnswer(string? answer) =>
        string.IsNullOrWhiteSpace(answer)
            ? string.Empty
            : answer.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

    private static bool TryParseNumericAnswer(string? answer, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(answer))
            return false;

        var normalized = answer.Trim();
        if (normalized.Contains('='))
            normalized = normalized[(normalized.IndexOf('=') + 1)..];

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseFraction(string answer, out FractionValue value)
    {
        var match = FractionAnswerPattern().Match(answer);
        if (!match.Success)
        {
            value = default;
            return false;
        }

        var numerator = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
        var denominator = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
        value = NormalizeFraction(numerator, denominator);
        return true;
    }

    private static FractionValue NormalizeFraction(int numerator, int denominator)
    {
        if (denominator == 0)
            return new FractionValue(numerator, denominator);

        var divisor = GreatestCommonDivisor(numerator, denominator);
        numerator /= divisor;
        denominator /= divisor;
        if (denominator < 0)
        {
            numerator *= -1;
            denominator *= -1;
        }

        return new FractionValue(numerator, denominator);
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var tmp = a % b;
            a = b;
            b = tmp;
        }

        return a == 0 ? 1 : a;
    }

    private readonly record struct FractionValue(int Numerator, int Denominator);

    [GeneratedRegex(@"^\s*(?<ln>-?\d+)\s*/\s*(?<ld>\d+)\s*(?<op>[+-])\s*(?<rn>-?\d+)\s*/\s*(?<rd>\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex FractionOperationPattern();

    [GeneratedRegex(@"^\s*(?<coef>-?\d*)x(?<offset>\s*[+-]\s*\d+)?\s*=\s*(?<rhs>-?\d+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LinearEquationPattern();

    [GeneratedRegex(@"^\s*(?<a>-?\d+)x\^2\s*(?<b>[+-]\s*\d+)x\s*(?<c>[+-]\s*\d+)\s*=\s*0\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex QuadraticPattern();

    [GeneratedRegex(@"^\s*(?<n>-?\d+)\s*/\s*(?<d>-?\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex FractionAnswerPattern();
}
