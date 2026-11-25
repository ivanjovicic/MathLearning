using System.Globalization;
using System.Text.RegularExpressions;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

/// <summary>
/// Fail-closed admission for the reviewed multiplication pack. An approval hash is
/// supplied only by server configuration after an independent content review.
/// Client-supplied approval fields are deliberately absent from the request.
/// </summary>
public sealed partial class MathVisualContentPolicy
{
    public const string PackId = "tablica_mnozenja";
    private readonly HashSet<string> approvedContentHashes;
    private readonly HashSet<int> packSubtopicIds;

    public MathVisualContentPolicy(
        IEnumerable<string>? approvedContentHashes = null,
        IEnumerable<int>? packSubtopicIds = null)
    {
        this.approvedContentHashes = new HashSet<string>(
            approvedContentHashes ?? [], StringComparer.OrdinalIgnoreCase);
        this.packSubtopicIds = new HashSet<int>(packSubtopicIds ?? []);
    }

    public static string ComputeContentHash(QuestionAuthoringRequest request)
        => QuestionAuthoringContentSupport.ComputeContentHash(request);

    public bool IsApprovedHash(string? hash)
        => hash is not null && approvedContentHashes.Contains(hash);

    public IReadOnlyCollection<int> PackSubtopicIds => packSubtopicIds;

    public IReadOnlyList<ValidationIssueDto> Validate(QuestionAuthoringRequest request, string contentHash)
    {
        var issues = new List<ValidationIssueDto>();
        if (request.Text is null || request.Options is null || request.Steps is null || request.Hints is null ||
            request.Options.Any(x => x is null || x.Text is null) ||
            request.Steps.Any(x => x is null || x.Text is null) ||
            request.Hints.Any(x => x is null || x.Key is null || x.Text is null) ||
            request.MathVisualPlan is { Groups: null })
        {
            issues.Add(Error("mathvisual.invalid_snapshot", "Required authored content fields are missing.", "mathVisualPlan"));
            return issues;
        }

        var plan = request.MathVisualPlan;
        var isDesignatedPackSubtopic = packSubtopicIds.Contains(request.SubtopicId);
        if (isDesignatedPackSubtopic && request.ContentPackId != PackId)
        {
            issues.Add(Error("mathvisual.pack_required", "This subtopic requires reviewed pack metadata.", "contentPackId"));
        }
        if (request.ContentPackId == PackId && !isDesignatedPackSubtopic)
        {
            issues.Add(Error("mathvisual.subtopic_unapproved", "The content pack has no approved server-side subtopic assignment.", "subtopicId"));
        }
        if (plan is not null && !string.Equals(request.ContentPackId, PackId, StringComparison.Ordinal))
        {
            issues.Add(Error("mathvisual.pack_required", "A visual plan requires the reviewed multiplication pack.", "contentPackId"));
        }

        if (!string.Equals(request.ContentPackId, PackId, StringComparison.Ordinal))
        {
            return issues;
        }

        if (request.Steps.Count == 0)
        {
            issues.Add(Error("mathvisual.steps_required", "Reviewed multiplication content requires authored steps.", "steps"));
        }

        if (HasUnsafeTeachingMarkup(request))
        {
            issues.Add(Error("mathvisual.markup_unsupported", "Reviewed teaching content cannot contain markup, executable URLs or external images.", "explanation"));
        }

        if (!approvedContentHashes.Contains(contentHash))
        {
            issues.Add(Error("mathvisual.review_missing", "The exact content hash has no server-side teacher approval.", "contentPackId"));
        }

        if (request.ContentLocale is not "sr")
        {
            issues.Add(Error("mathvisual.locale_unsupported", "The first reviewed pack supports only Serbian content.", "contentLocale"));
        }

        if (!TryParseProductPrompt(request.Text, out var promptRows, out var promptColumns) ||
            promptRows is < 2 or > 9 || promptColumns is < 2 or > 9)
        {
            issues.Add(Error("mathvisual.prompt_unsupported", "The reviewed pack requires an ordered 2–9 product prompt.", "text"));
            return issues;
        }

        var expectedProduct = promptRows * promptColumns;
        if (request.Hints.Count > 0)
        {
            issues.Add(Error("mathvisual.pre_hint_unsupported", "Pre-answer hints for this pack are not approved.", "hints"));
        }
        if (request.SemanticsAltText is not null &&
            Regex.IsMatch(request.SemanticsAltText, $@"(?<!\d){expectedProduct}(?!\d)", RegexOptions.CultureInvariant))
        {
            issues.Add(Error("mathvisual.pre_semantics_reveal", "Pre-answer semantics contain the product.", "semanticsAltText"));
        }
        if (request.Options.Any(option =>
                option.SemanticsAltText is not null &&
                Regex.IsMatch(option.SemanticsAltText, $@"(?<!\d){expectedProduct}(?!\d)", RegexOptions.CultureInvariant) &&
                NormalizeMathText(option.SemanticsAltText).Trim().Trim('$') !=
                NormalizeMathText(option.Text).Trim().Trim('$')))
        {
            issues.Add(Error("mathvisual.pre_option_semantics_reveal", "Option semantics add answer-bearing text before settlement.", "options.semanticsAltText"));
        }
        if (!int.TryParse(request.CorrectAnswer, NumberStyles.None, CultureInfo.InvariantCulture, out var answer) ||
            answer != expectedProduct ||
            (request.Type == "multiple_choice" && request.Options.Count(x => x.IsCorrect && x.Text == expectedProduct.ToString(CultureInfo.InvariantCulture)) != 1))
        {
            issues.Add(Error("mathvisual.answer_mismatch", "The expected answer or correct option differs from the prompt product.", "correctAnswer"));
        }

        if (HasFalseEquation(request, promptRows, promptColumns))
        {
            issues.Add(Error("mathvisual.contradictory_equation", "Reviewed explanation contains a false arithmetic equation.", "steps"));
        }

        if (plan is null)
        {
            // An approved authored text-only item is allowed; no generated visual fallback.
            return issues;
        }

        if (plan.SchemaVersion != 1 || plan.TemplateVersion != 1 ||
            plan.Operation is not "multiply" || plan.TaskShape is not "product" ||
            plan.Locale != request.ContentLocale ||
            plan.TemplateId is not ("directArray" or "splitFive"))
        {
            issues.Add(Error("mathvisual.schema_unsupported", "Visual plan schema, task shape, locale or template is unsupported.", "mathVisualPlan"));
        }

        if (plan.Rows is < 2 or > 9 || plan.Columns is < 2 or > 9 || plan.Rows * plan.Columns > 81)
        {
            issues.Add(Error("mathvisual.factors_out_of_range", "Array factors must be integers from 2 to 9.", "mathVisualPlan"));
            return issues;
        }

        if (promptRows != plan.Rows || promptColumns != plan.Columns)
        {
            issues.Add(Error("mathvisual.prompt_mismatch", "The prompt does not state the same ordered factors as the visual plan.", "text"));
        }

        var product = plan.Rows * plan.Columns;

        var groups = plan.Groups;
        var validGroups = groups is { Count: > 0 and <= 2 } &&
                          groups.All(x => x > 0 && x <= plan.Rows) &&
                          groups.Sum(x => (long)x) == plan.Rows &&
                          groups.Sum(x => (long)x * plan.Columns) == product;
        if (plan.TemplateId == "directArray")
        {
            validGroups &= groups is { Count: 1 } && groups[0] == plan.Rows;
        }
        else if (plan.TemplateId == "splitFive")
        {
            validGroups &= plan.Rows > 5 && groups is { Count: 2 } && groups[0] == 5 && groups[1] == plan.Rows - 5;
        }

        if (!validGroups)
        {
            issues.Add(Error("mathvisual.geometry_mismatch", "Groups must partition the ordered rows and sum to the exact product.", "mathVisualPlan.groups"));
        }

        if (request.Steps.Count > 0 && !StepsMatchPlan(request, plan))
        {
            issues.Add(Error("mathvisual.steps_mismatch", "Authored equations do not match the ordered array and partial products.", "steps"));
        }

        return issues;
    }

    private static bool StepsMatchPlan(QuestionAuthoringRequest request, MathVisualPlanDto plan)
    {
        var equations = request.Steps.OrderBy(x => x.Order)
            .Select(x => Regex.Replace(NormalizeMathText(x.Text), @"\s+", string.Empty))
            .ToArray();
        var product = plan.Rows * plan.Columns;
        if (plan.TemplateId == "directArray")
        {
            return equations.Any(x => x.Contains($"{plan.Rows}×{plan.Columns}={product}", StringComparison.Ordinal));
        }

        var remainder = plan.Rows - 5;
        return equations.Length >= 3 &&
               equations[0].Contains($"5×{plan.Columns}={5 * plan.Columns}", StringComparison.Ordinal) &&
               equations[1].Contains($"{remainder}×{plan.Columns}={remainder * plan.Columns}", StringComparison.Ordinal) &&
               equations[2].Contains($"{5 * plan.Columns}+{remainder * plan.Columns}={product}", StringComparison.Ordinal);
    }

    private static bool HasFalseEquation(QuestionAuthoringRequest request, int rows, int columns)
    {
        var explanationFields = request.Steps.SelectMany(x => new[] { x.Text, x.Hint, x.SemanticsAltText })
            .Concat(new[] { request.Explanation, request.SemanticsAltText })
            .Concat(request.Hints.SelectMany(x => new[] { x.Text, x.SemanticsAltText }));
        var text = string.Join(" ", explanationFields.Where(x => !string.IsNullOrWhiteSpace(x)));
        text = NormalizeMathText(text);
        foreach (Match phrase in LegacyGroupingPhrase().Matches(text))
        {
            if (phrase.Groups[1].Value != columns.ToString(CultureInfo.InvariantCulture) ||
                phrase.Groups[2].Value != rows.ToString(CultureInfo.InvariantCulture))
            {
                return true;
            }
        }
        foreach (Match match in Equation().Matches(text))
        {
            if (!int.TryParse(match.Groups[1].Value, out var a) ||
                !int.TryParse(match.Groups[3].Value, out var b) ||
                !int.TryParse(match.Groups[4].Value, out var stated))
            {
                return true;
            }
            var actual = match.Groups[2].Value switch
            {
                "+" => a + b,
                "-" or "−" => a - b,
                "×" or "*" => a * b,
                "÷" or "/" when b != 0 && a % b == 0 => a / b,
                _ => int.MinValue
            };
            if (actual != stated)
            {
                return true;
            }
        }
        if (UnsupportedNumericEquation().IsMatch(text))
        {
            return true;
        }
        return false;
    }

    private static bool HasUnsafeTeachingMarkup(QuestionAuthoringRequest request)
    {
        var fields = new[] { request.Text, request.Explanation, request.SemanticsAltText }
            .Concat(request.Options.SelectMany(x => new[] { x.Text, x.SemanticsAltText }))
            .Concat(request.Hints.SelectMany(x => new[] { x.Text, x.SemanticsAltText }))
            .Concat(request.Steps.SelectMany(x => new[] { x.Text, x.Hint, x.SemanticsAltText }));
        return fields.Where(x => !string.IsNullOrWhiteSpace(x)).Any(x =>
            HtmlOrSvgMarkup().IsMatch(x!) || ExecutableOrDataUrl().IsMatch(x!) || ExternalImage().IsMatch(x!));
    }

    public static bool TryParseProductPrompt(string text, out int rows, out int columns)
    {
        var match = ProductPrompt().Match(NormalizeMathText(text).Trim());
        rows = match.Success && int.TryParse(match.Groups[1].Value, out var r) ? r : 0;
        columns = match.Success && int.TryParse(match.Groups[2].Value, out var c) ? c : 0;
        return match.Success;
    }

    private static ValidationIssueDto Error(string id, string message, string field)
        => new(ValidationStageNames.PublishGuard, ValidationIssueSeverities.Error, id, message, field);

    private static string NormalizeMathText(string text)
        => text.Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(@"\times", "×", StringComparison.Ordinal)
            .Replace(@"\cdot", "×", StringComparison.Ordinal);

    [GeneratedRegex(@"^(?:Koliko je\s+)?(\d+)\s*[×*]\s*(\d+)\s*(?:=\s*\?)?\s*\??$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductPrompt();

    [GeneratedRegex(@"(?<![\d.,])(\d+)\s*([×*+÷/−-])\s*(\d+)\s*=\s*(\d+)(?![\d.,])", RegexOptions.CultureInvariant)]
    private static partial Regex Equation();

    [GeneratedRegex(@"\b(?:saberi|sabiramo)\s+(\d+)\s*,\s*(\d+)\s+puta\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyGroupingPhrase();

    [GeneratedRegex(@"\d+[.,]\d+\s*[×*+÷/−-]\s*\d+(?:[.,]\d+)?\s*=|\d+\s*[×*+÷/−-]\s*\d+(?:[.,]\d+)?\s*=\s*\d+[.,]\d+", RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedNumericEquation();

    [GeneratedRegex(@"<\s*/?\s*[A-Za-z][^>]*>|<\s*![A-Z]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlOrSvgMarkup();

    [GeneratedRegex(@"(?:javascript|data)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExecutableOrDataUrl();

    [GeneratedRegex(@"!\[[^\]]*\]\(\s*(?:https?:|//|data:)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalImage();
}
