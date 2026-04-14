using System.Globalization;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using MathLearning.Domain.Explanations;

namespace MathLearning.Api.Services;

public sealed partial class MathReasoningGraphEngine : IMathReasoningGraphEngine
{
    public MathReasoningGraph Build(MathProblemDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var text = Normalize(descriptor.ProblemText);
        if (TryBuildFractionGraph(text, descriptor, out var fractionGraph))
            return fractionGraph;
        if (TryBuildLinearEquationGraph(text, descriptor, out var linearGraph))
            return linearGraph;
        if (TryBuildQuadraticGraph(text, descriptor, out var quadraticGraph))
            return quadraticGraph;
        if (TryBuildArithmeticGraph(text, descriptor, out var arithmeticGraph))
            return arithmeticGraph;

        return BuildFallbackGraph(text, descriptor);
    }

    private static bool TryBuildFractionGraph(string problemText, MathProblemDescriptor descriptor, out MathReasoningGraph graph)
    {
        var match = FractionOperationPattern().Match(problemText);
        if (!match.Success)
        {
            graph = default!;
            return false;
        }

        var leftNumerator = int.Parse(match.Groups["ln"].Value, CultureInfo.InvariantCulture);
        var leftDenominator = int.Parse(match.Groups["ld"].Value, CultureInfo.InvariantCulture);
        var rightNumerator = int.Parse(match.Groups["rn"].Value, CultureInfo.InvariantCulture);
        var rightDenominator = int.Parse(match.Groups["rd"].Value, CultureInfo.InvariantCulture);
        var operation = match.Groups["op"].Value;
        var context = descriptor.Context;

        var root = CreateNode(
            problemText,
            ReasoningRule.ParseProblem,
            context,
            Explain(descriptor.Language,
                "We start by checking whether the fractions already have a common denominator.",
                "Prvo proveravamo da li razlomci već imaju zajednički imenilac."));

        var nodes = new List<MathReasoningNode> { root };

        var lcm = LeastCommonMultiple(leftDenominator, rightDenominator);
        var scaledLeft = leftNumerator * (lcm / leftDenominator);
        var scaledRight = rightNumerator * (lcm / rightDenominator);

        var commonDenominatorExpression = lcm == leftDenominator && lcm == rightDenominator
            ? $"{leftNumerator}/{leftDenominator} {operation} {rightNumerator}/{rightDenominator}"
            : $"{scaledLeft}/{lcm} {operation} {scaledRight}/{lcm}";

        var denominatorNode = CreateNode(
            commonDenominatorExpression,
            ReasoningRule.AddFractions,
            context,
            lcm == leftDenominator && lcm == rightDenominator
                ? Explain(descriptor.Language,
                    $"The denominator is already {lcm}, so we keep it and work with the numerators.",
                    $"Imenilac je već {lcm}, pa njega zadržavamo i računamo brojnioce.")
                : Explain(descriptor.Language,
                    $"We rewrite both fractions with the common denominator {lcm}.",
                    $"Oba razlomka prepisujemo sa zajedničkim imeniocem {lcm}."),
            formulaReferenceId: "fraction_addition_rule");
        root.AddChild(denominatorNode);
        nodes.Add(denominatorNode);

        var combinedNumerator = operation == "+" ? scaledLeft + scaledRight : scaledLeft - scaledRight;
        var combinedNode = CreateNode(
            $"{combinedNumerator}/{lcm}",
            ReasoningRule.AddNumerators,
            context,
            operation == "+"
                ? Explain(descriptor.Language,
                    $"Now we add the numerators: {scaledLeft} + {scaledRight} = {combinedNumerator}.",
                    $"Sada sabiramo brojnioce: {scaledLeft} + {scaledRight} = {combinedNumerator}.")
                : Explain(descriptor.Language,
                    $"Now we subtract the numerators: {scaledLeft} - {scaledRight} = {combinedNumerator}.",
                    $"Sada oduzimamo brojnioce: {scaledLeft} - {scaledRight} = {combinedNumerator}."));
        denominatorNode.AddChild(combinedNode);
        nodes.Add(combinedNode);

        var reduced = ReduceFraction(combinedNumerator, lcm);
        MathReasoningNode latestNode = combinedNode;
        if (reduced.Numerator != combinedNumerator || reduced.Denominator != lcm)
        {
            var simplifiedNode = CreateNode(
                reduced.ToDisplayString(),
                ReasoningRule.SimplifyFraction,
                context,
                Explain(descriptor.Language,
                    $"We simplify the fraction by dividing numerator and denominator by {reduced.Divisor}.",
                    $"Razlomak skraćujemo deljenjem brojioca i imenioca sa {reduced.Divisor}."),
                formulaReferenceId: "fraction_simplification_rule");
            combinedNode.AddChild(simplifiedNode);
            nodes.Add(simplifiedNode);
            latestNode = simplifiedNode;
        }

        var finalExpression = reduced.Denominator == 1
            ? reduced.Numerator.ToString(CultureInfo.InvariantCulture)
            : reduced.ToDisplayString();

        var finalNode = CreateNode(
            finalExpression,
            ReasoningRule.FinalizeResult,
            context,
            Explain(descriptor.Language,
                $"The final result is {finalExpression}.",
                $"Konačan rezultat je {finalExpression}."));
        latestNode.AddChild(finalNode);
        nodes.Add(finalNode);

        graph = new MathReasoningGraph(root, nodes);
        return true;
    }

    private static bool TryBuildArithmeticGraph(string problemText, MathProblemDescriptor descriptor, out MathReasoningGraph graph)
    {
        var match = ArithmeticPattern().Match(problemText);
        if (!match.Success)
        {
            graph = default!;
            return false;
        }

        var left = decimal.Parse(match.Groups["left"].Value, CultureInfo.InvariantCulture);
        var right = decimal.Parse(match.Groups["right"].Value, CultureInfo.InvariantCulture);
        var op = match.Groups["op"].Value;
        var result = op switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" or "×" => left * right,
            "/" when right != 0 => left / right,
            _ => decimal.Zero
        };

        if (op == "/" && right == 0)
        {
            graph = BuildFallbackGraph(problemText, descriptor);
            return true;
        }

        var root = CreateNode(
            problemText,
            ReasoningRule.ParseProblem,
            descriptor.Context,
            Explain(descriptor.Language,
                "We identify the operation and compute it directly.",
                "Prepoznajemo računsku operaciju i računamo je direktno."));

        var calculationNode = CreateNode(
            $"{ExplanationEngineSupport.FormatNumber(left)} {op} {ExplanationEngineSupport.FormatNumber(right)} = {ExplanationEngineSupport.FormatNumber(result)}",
            ReasoningRule.EvaluateArithmetic,
            descriptor.Context,
            Explain(descriptor.Language,
                $"Calculating gives {ExplanationEngineSupport.FormatNumber(result)}.",
                $"Računanjem dobijamo {ExplanationEngineSupport.FormatNumber(result)}."));
        root.AddChild(calculationNode);

        var finalNode = CreateNode(
            ExplanationEngineSupport.FormatNumber(result),
            ReasoningRule.FinalizeResult,
            descriptor.Context,
            Explain(descriptor.Language,
                $"The final result is {ExplanationEngineSupport.FormatNumber(result)}.",
                $"Konačan rezultat je {ExplanationEngineSupport.FormatNumber(result)}."));
        calculationNode.AddChild(finalNode);

        graph = new MathReasoningGraph(root, [root, calculationNode, finalNode]);
        return true;
    }

    private static bool TryBuildLinearEquationGraph(string problemText, MathProblemDescriptor descriptor, out MathReasoningGraph graph)
    {
        var match = LinearEquationPattern().Match(problemText);
        if (!match.Success)
        {
            graph = default!;
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

        if (coefficient == 0)
        {
            graph = BuildFallbackGraph(problemText, descriptor);
            return true;
        }

        var root = CreateNode(
            problemText,
            ReasoningRule.ParseProblem,
            descriptor.Context,
            Explain(descriptor.Language,
                "We isolate the variable by undoing operations in reverse order.",
                "Promenljivu izdvajamo tako što operacije poništavamo obrnutim redosledom."));
        var nodes = new List<MathReasoningNode> { root };

        MathReasoningNode latest = root;
        var workingRhs = rhs;
        if (offset != 0)
        {
            workingRhs -= offset;
            var normalized = CreateNode(
                $"{FormatCoefficient(coefficient)}x = {workingRhs}",
                ReasoningRule.NormalizeEquation,
                descriptor.Context,
                offset > 0
                    ? Explain(descriptor.Language,
                        $"Subtract {offset} from both sides to remove the constant term.",
                        $"Oduzimamo {offset} sa obe strane da uklonimo konstantni član.")
                    : Explain(descriptor.Language,
                        $"Add {Math.Abs(offset)} to both sides to remove the negative constant.",
                        $"Dodajemo {Math.Abs(offset)} sa obe strane da uklonimo negativnu konstantu."));
            latest.AddChild(normalized);
            nodes.Add(normalized);
            latest = normalized;
        }

        var solution = (decimal)workingRhs / coefficient;
        if (coefficient != 1)
        {
            var isolateNode = CreateNode(
                $"x = {ExplanationEngineSupport.FormatNumber(solution)}",
                ReasoningRule.IsolateVariable,
                descriptor.Context,
                Explain(descriptor.Language,
                    $"Divide both sides by {coefficient} to isolate x.",
                    $"Delimo obe strane sa {coefficient} da izdvojimo x."),
                formulaReferenceId: "linear_equation_isolation");
            latest.AddChild(isolateNode);
            nodes.Add(isolateNode);
            latest = isolateNode;
        }

        var finalNode = CreateNode(
            $"x = {ExplanationEngineSupport.FormatNumber(solution)}",
            ReasoningRule.FinalizeResult,
            descriptor.Context,
            Explain(descriptor.Language,
                $"The solution is x = {ExplanationEngineSupport.FormatNumber(solution)}.",
                $"Rešenje je x = {ExplanationEngineSupport.FormatNumber(solution)}."));
        latest.AddChild(finalNode);
        nodes.Add(finalNode);

        graph = new MathReasoningGraph(root, nodes);
        return true;
    }

    private static bool TryBuildQuadraticGraph(string problemText, MathProblemDescriptor descriptor, out MathReasoningGraph graph)
    {
        var match = QuadraticPattern().Match(problemText);
        if (!match.Success)
        {
            graph = default!;
            return false;
        }

        var a = int.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
        var b = int.Parse(match.Groups["b"].Value.Replace(" ", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        var c = int.Parse(match.Groups["c"].Value.Replace(" ", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

        if (a == 0)
        {
            graph = BuildFallbackGraph(problemText, descriptor);
            return true;
        }

        var root = CreateNode(
            problemText,
            ReasoningRule.ParseProblem,
            descriptor.Context,
            Explain(descriptor.Language,
                "This is a quadratic equation, so we apply the quadratic formula.",
                "Ovo je kvadratna jednačina, pa primenjujemo kvadratnu formulu."));

        var discriminant = b * b - (4 * a * c);
        var formulaExpression = "x = (-b ± sqrt(b^2 - 4ac)) / (2a)";
        var formulaNode = CreateNode(
            formulaExpression,
            ReasoningRule.ApplyFormula,
            descriptor.Context,
            Explain(descriptor.Language,
                $"Substitute a = {a}, b = {b}, and c = {c} into the quadratic formula.",
                $"Uvrštavamo a = {a}, b = {b} i c = {c} u kvadratnu formulu."),
            formulaReferenceId: "quadratic_formula");
        root.AddChild(formulaNode);

        var finalExpression = discriminant switch
        {
            < 0 => Explain(descriptor.Language, "No real roots", "Nema realnih rešenja"),
            _ when IsPerfectSquare(discriminant) =>
                BuildQuadraticResult(a, b, discriminant),
            _ => $"x = ({-b} ± sqrt({discriminant})) / {2 * a}"
        };

        var finalNode = CreateNode(
            finalExpression,
            ReasoningRule.FinalizeResult,
            descriptor.Context,
            Explain(descriptor.Language,
                $"The final result is {finalExpression}.",
                $"Konačan rezultat je {finalExpression}."));
        formulaNode.AddChild(finalNode);

        graph = new MathReasoningGraph(root, [root, formulaNode, finalNode]);
        return true;
    }

    private static MathReasoningGraph BuildFallbackGraph(string problemText, MathProblemDescriptor descriptor)
    {
        var root = CreateNode(
            problemText,
            ReasoningRule.Unknown,
            descriptor.Context,
            Explain(descriptor.Language,
                "This problem needs a custom explanation, so we keep the original expression and provide guided hints.",
                "Za ovaj zadatak je potrebno prilagođeno objašnjenje, pa zadržavamo originalni izraz i dajemo vođene hintove."));
        var final = CreateNode(
            problemText,
            ReasoningRule.FinalizeResult,
            descriptor.Context,
            Explain(descriptor.Language,
                "Use the hints below to continue the reasoning manually.",
                "Iskoristi hintove ispod da ručno nastaviš rezonovanje."));
        root.AddChild(final);
        return new MathReasoningGraph(root, [root, final]);
    }

    private static MathReasoningNode CreateNode(
        string expression,
        ReasoningRule rule,
        MathContext context,
        string narrative,
        string? formulaReferenceId = null)
    {
        return new MathReasoningNode(
            expression,
            rule,
            context,
            narrative,
            ExplanationEngineSupport.ToLatex(expression),
            ExplanationEngineSupport.ToMathMl(expression),
            formulaReferenceId);
    }

    private static string Normalize(string problemText) =>
        Regex.Replace(problemText?.Trim() ?? string.Empty, @"\s+", " ");

    private static string Explain(string language, string english, string serbian) =>
        ExplanationEngineSupport.IsSerbian(language) ? serbian : english;

    private static string FormatCoefficient(int coefficient) => coefficient switch
    {
        1 => string.Empty,
        -1 => "-",
        _ => coefficient.ToString(CultureInfo.InvariantCulture)
    };

    private static bool IsPerfectSquare(int value)
    {
        if (value < 0)
            return false;

        var root = (int)Math.Sqrt(value);
        return root * root == value;
    }

    private static string BuildQuadraticResult(int a, int b, int discriminant)
    {
        var sqrt = (decimal)Math.Sqrt(discriminant);
        var denominator = 2m * a;
        var x1 = (-b + sqrt) / denominator;
        var x2 = (-b - sqrt) / denominator;
        return $"x = {ExplanationEngineSupport.FormatNumber(x1)} or x = {ExplanationEngineSupport.FormatNumber(x2)}";
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

    private static int LeastCommonMultiple(int a, int b) => Math.Abs(a * b) / GreatestCommonDivisor(a, b);

    private static ReducedFraction ReduceFraction(int numerator, int denominator)
    {
        if (numerator == 0)
            return new ReducedFraction(0, 1, Math.Abs(denominator));

        var divisor = GreatestCommonDivisor(numerator, denominator);
        var reducedNumerator = numerator / divisor;
        var reducedDenominator = denominator / divisor;
        if (reducedDenominator < 0)
        {
            reducedNumerator *= -1;
            reducedDenominator *= -1;
        }

        return new ReducedFraction(reducedNumerator, reducedDenominator, divisor);
    }

    private readonly record struct ReducedFraction(int Numerator, int Denominator, int Divisor)
    {
        public string ToDisplayString() => $"{Numerator}/{Denominator}";
    }

    [GeneratedRegex(@"^\s*(?<ln>-?\d+)\s*/\s*(?<ld>\d+)\s*(?<op>[+-])\s*(?<rn>-?\d+)\s*/\s*(?<rd>\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex FractionOperationPattern();

    [GeneratedRegex(@"^\s*(?<left>-?\d+(?:\.\d+)?)\s*(?<op>[+\-*/×])\s*(?<right>-?\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled)]
    private static partial Regex ArithmeticPattern();

    [GeneratedRegex(@"^\s*(?<coef>-?\d*)x(?<offset>\s*[+-]\s*\d+)?\s*=\s*(?<rhs>-?\d+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LinearEquationPattern();

    [GeneratedRegex(@"^\s*(?<a>-?\d+)x\^2\s*(?<b>[+-]\s*\d+)x\s*(?<c>[+-]\s*\d+)\s*=\s*0\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex QuadraticPattern();
}
