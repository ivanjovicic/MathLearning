using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;
using System.Text.RegularExpressions;

namespace MathLearning.Application.Helpers;

public static class StepEngine
{
    /// <summary>
    /// Generates step-by-step explanation for a question.
    /// Uses stored steps if available, otherwise generates dynamically from question text.
    /// </summary>
    public static List<StepExplanationDto> GetSteps(Question question, string userLang)
    {
        // 1. Use stored steps if they exist
        if (question.Steps.Count > 0)
        {
            return question.Steps
                .OrderBy(s => s.StepIndex)
                .Select(s => new StepExplanationDto(
                    GetStepText(s, userLang),
                    GetStepHint(s, userLang),
                    s.Highlight,
                    s.TextFormat,
                    s.HintFormat,
                    s.TextRenderMode,
                    s.HintRenderMode,
                    TranslationHelper.GetStepSemanticsAltText(s, userLang)
                ))
                .ToList();
        }

        // 2. Try to generate dynamically from question text/type
        var generated = GenerateFromQuestion(question, userLang);
        if (generated.Count > 0)
            return generated;

        // 3. Fallback: use explanation as a single step
        var explanation = TranslationHelper.GetExplanation(question, userLang);
        if (!string.IsNullOrWhiteSpace(explanation))
        {
            return [new StepExplanationDto(
                explanation,
                null,
                true,
                SemanticsAltText: TranslationHelper.ResolveSemanticsAltText(
                    null,
                    explanation,
                    question.ExplanationFormat))];
        }

        return [];
    }

    private static string GetStepText(QuestionStep step, string userLang)
    {
        if (string.Equals(userLang, TranslationHelper.DefaultLang, StringComparison.OrdinalIgnoreCase))
            return step.Text;

        var translation = step.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));
        if (translation != null) return translation.Text;

        if (!string.Equals(userLang, TranslationHelper.FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = step.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, TranslationHelper.FallbackLang, StringComparison.OrdinalIgnoreCase));
            if (translation != null) return translation.Text;
        }

        return step.Text;
    }

    private static string? GetStepHint(QuestionStep step, string userLang)
    {
        if (string.Equals(userLang, TranslationHelper.DefaultLang, StringComparison.OrdinalIgnoreCase))
            return step.Hint;

        var translation = step.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));
        if (translation?.Hint != null) return translation.Hint;

        if (!string.Equals(userLang, TranslationHelper.FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = step.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, TranslationHelper.FallbackLang, StringComparison.OrdinalIgnoreCase));
            if (translation?.Hint != null) return translation.Hint;
        }

        return step.Hint;
    }

    /// <summary>
    /// Dynamically generates steps by parsing the question text and detecting the operation type.
    /// </summary>
    private static List<StepExplanationDto> GenerateFromQuestion(Question question, string userLang)
    {
        var text = question.Text;
        bool isSr = string.Equals(userLang, "sr", StringComparison.OrdinalIgnoreCase);

        // Try addition: "Koliko je A + B?" or "What is A + B?"
        var addMatch = Regex.Match(text, @"(\d+)\s*\+\s*(\d+)");
        if (addMatch.Success)
        {
            int a = int.Parse(addMatch.Groups[1].Value);
            int b = int.Parse(addMatch.Groups[2].Value);
            return isSr ? ForAdditionSr(a, b) : ForAdditionEn(a, b);
        }

        // Try subtraction: "A - B" or "A − B"
        var subMatch = Regex.Match(text, @"(\d+)\s*[\-−]\s*(\d+)");
        if (subMatch.Success)
        {
            int a = int.Parse(subMatch.Groups[1].Value);
            int b = int.Parse(subMatch.Groups[2].Value);
            return isSr ? ForSubtractionSr(a, b) : ForSubtractionEn(a, b);
        }

        // Try multiplication: "A × B" or "A * B"
        var mulMatch = Regex.Match(text, @"(\d+)\s*[×\*]\s*(\d+)");
        if (mulMatch.Success)
        {
            int a = int.Parse(mulMatch.Groups[1].Value);
            int b = int.Parse(mulMatch.Groups[2].Value);
            return isSr ? ForMultiplicationSr(a, b) : ForMultiplicationEn(a, b);
        }

        // Try division: "A ÷ B" or "A / B"
        var divMatch = Regex.Match(text, @"(\d+)\s*[÷/]\s*(\d+)");
        if (divMatch.Success)
        {
            int a = int.Parse(divMatch.Groups[1].Value);
            int b = int.Parse(divMatch.Groups[2].Value);
            if (b != 0)
                return isSr ? ForDivisionSr(a, b) : ForDivisionEn(a, b);
        }

        // Try simple linear equation: "x + B = C" or "Ax = C"
        var eqMatch1 = Regex.Match(text, @"x\s*\+\s*(\d+)\s*=\s*(\d+)");
        if (eqMatch1.Success)
        {
            int b = int.Parse(eqMatch1.Groups[1].Value);
            int c = int.Parse(eqMatch1.Groups[2].Value);
            return isSr ? ForLinearAddSr(b, c) : ForLinearAddEn(b, c);
        }

        var eqMatch2 = Regex.Match(text, @"(\d+)x\s*=\s*(\d+)");
        if (eqMatch2.Success)
        {
            int a = int.Parse(eqMatch2.Groups[1].Value);
            int c = int.Parse(eqMatch2.Groups[2].Value);
            return isSr ? ForLinearMulSr(a, c) : ForLinearMulEn(a, c);
        }

        // Try "Ax - B = C" or "Ax + B = C"
        var eqMatch3 = Regex.Match(text, @"(\d+)x\s*([\+\-−])\s*(\d+)\s*=\s*(\d+)");
        if (eqMatch3.Success)
        {
            int a = int.Parse(eqMatch3.Groups[1].Value);
            string op = eqMatch3.Groups[2].Value;
            int b = int.Parse(eqMatch3.Groups[3].Value);
            int c = int.Parse(eqMatch3.Groups[4].Value);
            bool isSubtract = op == "-" || op == "−";
            return isSr ? ForLinearComplexSr(a, b, c, isSubtract) : ForLinearComplexEn(a, b, c, isSubtract);
        }

        return [];
    }

    // ────────────────────────────────────────────────
    // Addition
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForAdditionSr(int a, int b)
    {
        var tensA = (a / 10) * 10;
        var tensB = (b / 10) * 10;
        var onesA = a % 10;
        var onesB = b % 10;
        var onesSum = onesA + onesB;
        var tensSum = tensA + tensB;

        var steps = new List<StepExplanationDto>
        {
            new($"Razvoj broja {a} → {tensA} + {onesA}", null, false),
            new($"Razvoj broja {b} → {tensB} + {onesB}", null, false),
            new($"Saberi desetice: {tensA} + {tensB} = {tensSum}", null, false),
            new($"Saberi jedinice: {onesA} + {onesB} = {onesSum}", null, false),
        };

        if (onesSum >= 10)
        {
            steps.Add(new($"Prenesi deseticu: {tensSum} + {onesSum} = {tensSum + onesSum}", "Jedinice prelaze 10, dodaj 1 na desetice", false));
        }

        steps.Add(new($"Rezultat: {a} + {b} = {a + b}", null, true));
        return steps;
    }

    public static List<StepExplanationDto> ForAdditionEn(int a, int b)
    {
        var tensA = (a / 10) * 10;
        var tensB = (b / 10) * 10;
        var onesA = a % 10;
        var onesB = b % 10;
        var onesSum = onesA + onesB;
        var tensSum = tensA + tensB;

        var steps = new List<StepExplanationDto>
        {
            new($"Break down {a} → {tensA} + {onesA}", null, false),
            new($"Break down {b} → {tensB} + {onesB}", null, false),
            new($"Add tens: {tensA} + {tensB} = {tensSum}", null, false),
            new($"Add ones: {onesA} + {onesB} = {onesSum}", null, false),
        };

        if (onesSum >= 10)
        {
            steps.Add(new($"Carry over: {tensSum} + {onesSum} = {tensSum + onesSum}", "Ones exceed 10, add 1 to tens", false));
        }

        steps.Add(new($"Result: {a} + {b} = {a + b}", null, true));
        return steps;
    }

    // ────────────────────────────────────────────────
    // Subtraction
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForSubtractionSr(int a, int b)
    {
        var tensA = (a / 10) * 10;
        var tensB = (b / 10) * 10;
        var onesA = a % 10;
        var onesB = b % 10;

        var steps = new List<StepExplanationDto>
        {
            new($"Razvoj broja {a} → {tensA} + {onesA}", null, false),
            new($"Razvoj broja {b} → {tensB} + {onesB}", null, false),
        };

        if (onesA < onesB)
        {
            steps.Add(new($"Pozajmi deseticu: {tensA} postaje {tensA - 10}, a {onesA} postaje {onesA + 10}",
                "Jedinice umanjenog su manje, pozajmi od desetica", false));
            steps.Add(new($"Oduzmi desetice: {tensA - 10} − {tensB} = {tensA - 10 - tensB}", null, false));
            steps.Add(new($"Oduzmi jedinice: {onesA + 10} − {onesB} = {onesA + 10 - onesB}", null, false));
        }
        else
        {
            steps.Add(new($"Oduzmi desetice: {tensA} − {tensB} = {tensA - tensB}", null, false));
            steps.Add(new($"Oduzmi jedinice: {onesA} − {onesB} = {onesA - onesB}", null, false));
        }

        steps.Add(new($"Rezultat: {a} − {b} = {a - b}", null, true));
        return steps;
    }

    public static List<StepExplanationDto> ForSubtractionEn(int a, int b)
    {
        var tensA = (a / 10) * 10;
        var tensB = (b / 10) * 10;
        var onesA = a % 10;
        var onesB = b % 10;

        var steps = new List<StepExplanationDto>
        {
            new($"Break down {a} → {tensA} + {onesA}", null, false),
            new($"Break down {b} → {tensB} + {onesB}", null, false),
        };

        if (onesA < onesB)
        {
            steps.Add(new($"Borrow: {tensA} becomes {tensA - 10}, and {onesA} becomes {onesA + 10}",
                "Ones of minuend are smaller, borrow from tens", false));
            steps.Add(new($"Subtract tens: {tensA - 10} − {tensB} = {tensA - 10 - tensB}", null, false));
            steps.Add(new($"Subtract ones: {onesA + 10} − {onesB} = {onesA + 10 - onesB}", null, false));
        }
        else
        {
            steps.Add(new($"Subtract tens: {tensA} − {tensB} = {tensA - tensB}", null, false));
            steps.Add(new($"Subtract ones: {onesA} − {onesB} = {onesA - onesB}", null, false));
        }

        steps.Add(new($"Result: {a} − {b} = {a - b}", null, true));
        return steps;
    }

    // ────────────────────────────────────────────────
    // Multiplication
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForMultiplicationSr(int a, int b)
    {
        var steps = new List<StepExplanationDto>();

        if (a <= 10 && b <= 10)
        {
            // Simple table multiplication
            steps.Add(new($"{a} × {b} znači: saberi {a}, {b} puta", null, false));
            var parts = string.Join(" + ", Enumerable.Repeat(a.ToString(), b));
            steps.Add(new($"{parts} = {a * b}", null, false));
        }
        else
        {
            // Distributive approach
            var tensB = (b / 10) * 10;
            var onesB = b % 10;

            steps.Add(new($"Razbij {b} → {tensB} + {onesB}", null, false));
            steps.Add(new($"{a} × {tensB} = {a * tensB}", null, false));

            if (onesB > 0)
            {
                steps.Add(new($"{a} × {onesB} = {a * onesB}", null, false));
                steps.Add(new($"Saberi: {a * tensB} + {a * onesB} = {a * b}", null, false));
            }
        }

        steps.Add(new($"Rezultat: {a} × {b} = {a * b}", null, true));
        return steps;
    }

    public static List<StepExplanationDto> ForMultiplicationEn(int a, int b)
    {
        var steps = new List<StepExplanationDto>();

        if (a <= 10 && b <= 10)
        {
            steps.Add(new($"{a} × {b} means: add {a}, {b} times", null, false));
            var parts = string.Join(" + ", Enumerable.Repeat(a.ToString(), b));
            steps.Add(new($"{parts} = {a * b}", null, false));
        }
        else
        {
            var tensB = (b / 10) * 10;
            var onesB = b % 10;

            steps.Add(new($"Break down {b} → {tensB} + {onesB}", null, false));
            steps.Add(new($"{a} × {tensB} = {a * tensB}", null, false));

            if (onesB > 0)
            {
                steps.Add(new($"{a} × {onesB} = {a * onesB}", null, false));
                steps.Add(new($"Add: {a * tensB} + {a * onesB} = {a * b}", null, false));
            }
        }

        steps.Add(new($"Result: {a} × {b} = {a * b}", null, true));
        return steps;
    }

    // ────────────────────────────────────────────────
    // Division
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForDivisionSr(int a, int b)
    {
        int result = a / b;
        int remainder = a % b;

        var steps = new List<StepExplanationDto>
        {
            new($"Pitanje: koliko puta {b} staje u {a}?", null, false),
            new($"{b} × {result} = {b * result}", $"Probamo množenje sa {result}", false),
        };

        if (remainder > 0)
        {
            steps.Add(new($"Ostatak: {a} − {b * result} = {remainder}", null, false));
            steps.Add(new($"Rezultat: {a} ÷ {b} = {result} (ostatak {remainder})", null, true));
        }
        else
        {
            steps.Add(new($"Rezultat: {a} ÷ {b} = {result}", null, true));
        }

        return steps;
    }

    public static List<StepExplanationDto> ForDivisionEn(int a, int b)
    {
        int result = a / b;
        int remainder = a % b;

        var steps = new List<StepExplanationDto>
        {
            new($"Question: how many times does {b} fit into {a}?", null, false),
            new($"{b} × {result} = {b * result}", $"Try multiplying by {result}", false),
        };

        if (remainder > 0)
        {
            steps.Add(new($"Remainder: {a} − {b * result} = {remainder}", null, false));
            steps.Add(new($"Result: {a} ÷ {b} = {result} (remainder {remainder})", null, true));
        }
        else
        {
            steps.Add(new($"Result: {a} ÷ {b} = {result}", null, true));
        }

        return steps;
    }

    // ────────────────────────────────────────────────
    // Linear Equations: x + b = c
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForLinearAddSr(int b, int c)
    {
        int x = c - b;
        return
        [
            new($"Jednačina: x + {b} = {c}", null, false),
            new($"Oduzmi {b} sa obe strane", $"Cilj: izoluj x", false),
            new($"x = {c} − {b}", null, false),
            new($"x = {x}", null, true),
            new($"Provera: {x} + {b} = {c} ✓", null, false),
        ];
    }

    public static List<StepExplanationDto> ForLinearAddEn(int b, int c)
    {
        int x = c - b;
        return
        [
            new($"Equation: x + {b} = {c}", null, false),
            new($"Subtract {b} from both sides", "Goal: isolate x", false),
            new($"x = {c} − {b}", null, false),
            new($"x = {x}", null, true),
            new($"Check: {x} + {b} = {c} ✓", null, false),
        ];
    }

    // ────────────────────────────────────────────────
    // Linear Equations: ax = c
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForLinearMulSr(int a, int c)
    {
        int x = c / a;
        return
        [
            new($"Jednačina: {a}x = {c}", null, false),
            new($"Podeli obe strane sa {a}", $"Cilj: izoluj x", false),
            new($"x = {c} ÷ {a}", null, false),
            new($"x = {x}", null, true),
            new($"Provera: {a} × {x} = {c} ✓", null, false),
        ];
    }

    public static List<StepExplanationDto> ForLinearMulEn(int a, int c)
    {
        int x = c / a;
        return
        [
            new($"Equation: {a}x = {c}", null, false),
            new($"Divide both sides by {a}", "Goal: isolate x", false),
            new($"x = {c} ÷ {a}", null, false),
            new($"x = {x}", null, true),
            new($"Check: {a} × {x} = {c} ✓", null, false),
        ];
    }

    // ────────────────────────────────────────────────
    // Linear Equations: ax ± b = c
    // ────────────────────────────────────────────────
    public static List<StepExplanationDto> ForLinearComplexSr(int a, int b, int c, bool isSubtract)
    {
        int rightSide = isSubtract ? c + b : c - b;
        int x = rightSide / a;
        string op = isSubtract ? "−" : "+";
        string inverseOp = isSubtract ? "Dodaj" : "Oduzmi";

        return
        [
            new($"Jednačina: {a}x {op} {b} = {c}", null, false),
            new($"{inverseOp} {b} sa obe strane", $"Cilj: izoluj {a}x", false),
            new($"{a}x = {rightSide}", null, false),
            new($"Podeli obe strane sa {a}", $"Cilj: izoluj x", false),
            new($"x = {rightSide} ÷ {a}", null, false),
            new($"x = {x}", null, true),
        ];
    }

    public static List<StepExplanationDto> ForLinearComplexEn(int a, int b, int c, bool isSubtract)
    {
        int rightSide = isSubtract ? c + b : c - b;
        int x = rightSide / a;
        string op = isSubtract ? "−" : "+";
        string inverseOp = isSubtract ? "Add" : "Subtract";

        return
        [
            new($"Equation: {a}x {op} {b} = {c}", null, false),
            new($"{inverseOp} {b} from both sides", $"Goal: isolate {a}x", false),
            new($"{a}x = {rightSide}", null, false),
            new($"Divide both sides by {a}", "Goal: isolate x", false),
            new($"x = {rightSide} ÷ {a}", null, false),
            new($"x = {x}", null, true),
        ];
    }
}
