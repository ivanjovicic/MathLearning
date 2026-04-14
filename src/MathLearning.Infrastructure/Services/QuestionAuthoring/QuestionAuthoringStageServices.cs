using System.Text.Json;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

public sealed class MathContentLinter : IMathContentLinter
{
    public ContentLintResultDto Lint(QuestionAuthoringRequest request)
    {
        var issues = new List<ValidationIssueDto>();

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            issues.Add(Error("content.empty_question_text", "Question text must not be empty.", "text", "Provide non-empty problem text."));
        }

        if (QuestionAuthoringContentSupport.ContainsUnsafeMarkup(request.Text))
        {
            issues.Add(Error("content.unsafe_markup", "Raw HTML or script markup is not allowed.", "text", "Use plain text and math delimiters only."));
        }

        if (request.Options.Count == 0 && string.Equals(request.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("options.missing", "Multiple choice questions require answer options.", "options"));
        }

        var optionFingerprints = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < request.Options.Count; index++)
        {
            var option = request.Options[index];
            var fieldPath = $"options[{index}]";
            if (string.IsNullOrWhiteSpace(option.Text))
            {
                issues.Add(Error("options.empty", "Option text must not be empty.", fieldPath));
                continue;
            }

            if (QuestionAuthoringContentSupport.ContainsUnsafeMarkup(option.Text))
            {
                issues.Add(Error("options.unsafe_markup", "Option contains unsafe markup.", fieldPath));
            }

            var fingerprint = QuestionAuthoringContentSupport.NormalizeVisibleText(option.Text);
            if (optionFingerprints.TryGetValue(fingerprint, out var duplicateIndex))
            {
                issues.Add(Error(
                    "options.duplicate_visual",
                    $"Option is visually identical to option {duplicateIndex + 1} after normalization.",
                    fieldPath,
                    "Make duplicate options visually distinct."));
            }
            else
            {
                optionFingerprints[fingerprint] = index;
            }
        }

        var correctOptions = request.Options.Count(x => x.IsCorrect);
        var hasExplicitAnswer = !string.IsNullOrWhiteSpace(request.CorrectAnswer);
        if (!hasExplicitAnswer && correctOptions == 0)
        {
            issues.Add(Error("answers.missing_correct_answer", "A correct answer is required.", "correctAnswer"));
        }

        if (string.Equals(request.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            if (correctOptions == 0)
            {
                issues.Add(Error("answers.missing_correct_option", "Exactly one correct option is required for multiple choice questions.", "options"));
            }
            else if (correctOptions > 1)
            {
                issues.Add(Error("answers.multiple_correct_options", "Multiple choice questions cannot have multiple correct options.", "options"));
            }
        }

        var hintKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < request.Hints.Count; index++)
        {
            var hint = request.Hints[index];
            var fieldPath = $"hints[{index}]";
            if (string.IsNullOrWhiteSpace(hint.Key) || string.IsNullOrWhiteSpace(hint.Text))
            {
                issues.Add(Error("hints.invalid", "Hint objects require both key and text.", fieldPath));
            }

            if (!hintKeys.Add(hint.Key))
            {
                issues.Add(Error("hints.duplicate_key", $"Duplicate hint key '{hint.Key}'.", fieldPath));
            }

            if (QuestionAuthoringContentSupport.ContainsUnsafeMarkup(hint.Text))
            {
                issues.Add(Error("hints.unsafe_markup", "Hint contains unsafe markup.", fieldPath));
            }
        }

        if (request.RequireSteps && request.Steps.Count == 0)
        {
            issues.Add(Error("steps.required_missing", "This question requires at least one explanation step.", "steps"));
        }

        var expectedOrder = 1;
        foreach (var step in request.Steps.OrderBy(x => x.Order))
        {
            var fieldPath = $"steps[{step.Order}]";
            if (string.IsNullOrWhiteSpace(step.Text))
            {
                issues.Add(Error("steps.empty_text", "Step explanation text must not be empty.", fieldPath));
            }

            if (step.Order != expectedOrder)
            {
                issues.Add(Error(
                    "steps.non_contiguous_order",
                    $"Step order must be contiguous. Expected {expectedOrder} but got {step.Order}.",
                    fieldPath));
                expectedOrder = step.Order;
            }

            expectedOrder++;
        }

        foreach (var field in EnumerateFields(request))
        {
            if (string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            if (!QuestionAuthoringContentSupport.TrySegment(field.Value, out _, out var errorCode, out var errorMessage))
            {
                issues.Add(Error(errorCode ?? "content.invalid_math_delimiter", errorMessage ?? "Content has invalid math delimiters.", field.Path));
            }

            if (!QuestionAuthoringContentSupport.HasBalancedBraces(field.Value))
            {
                issues.Add(Error("content.unbalanced_braces", "Content contains unbalanced braces.", field.Path));
            }

            if (QuestionAuthoringContentSupport.HasSuspiciousFormatting(field.Value))
            {
                issues.Add(Warning("content.suspicious_formatting", "Content contains excessive blank lines or formatting noise.", field.Path));
            }

            var hasLatexCommands = QuestionAuthoringContentSupport.GetUnsupportedCommands(field.Value).Count > 0;
            if (field.Value.Contains('\\') && !ContainsAnyMathDelimiter(field.Value))
            {
                issues.Add(Error("content.latex_outside_math", "LaTeX commands must be placed inside math delimiters.", field.Path));
            }

            if (hasLatexCommands)
            {
                issues.Add(Error("latex.unsupported_command", "Content contains unsupported LaTeX commands.", field.Path));
            }
        }

        var isValid = issues.All(x => !string.Equals(x.Severity, ValidationIssueSeverities.Error, StringComparison.Ordinal));
        return new ContentLintResultDto(isValid, issues);
    }

    private static IEnumerable<(string Path, string? Value)> EnumerateFields(QuestionAuthoringRequest request)
    {
        yield return ("text", request.Text);
        yield return ("correctAnswer", request.CorrectAnswer);
        yield return ("explanation", request.Explanation);

        for (var index = 0; index < request.Options.Count; index++)
        {
            yield return ($"options[{index}].text", request.Options[index].Text);
        }

        for (var index = 0; index < request.Hints.Count; index++)
        {
            yield return ($"hints[{index}].text", request.Hints[index].Text);
        }

        for (var index = 0; index < request.Steps.Count; index++)
        {
            yield return ($"steps[{index}].text", request.Steps[index].Text);
            yield return ($"steps[{index}].hint", request.Steps[index].Hint);
        }
    }

    private static bool ContainsAnyMathDelimiter(string value)
        => value.Contains('$', StringComparison.Ordinal) ||
           value.Contains(@"\(", StringComparison.Ordinal) ||
           value.Contains(@"\[", StringComparison.Ordinal);

    private static ValidationIssueDto Error(string ruleId, string message, string? fieldPath = null, string? suggestion = null)
        => new(ValidationStageNames.Lint, ValidationIssueSeverities.Error, ruleId, message, fieldPath, suggestion);

    private static ValidationIssueDto Warning(string ruleId, string message, string? fieldPath = null, string? suggestion = null)
        => new(ValidationStageNames.Lint, ValidationIssueSeverities.Warning, ruleId, message, fieldPath, suggestion);
}

public sealed class LatexValidationService : ILatexValidationService
{
    public LatexValidationResultDto Validate(QuestionAuthoringRequest request)
    {
        var details = new List<LatexValidationDetailDto>();

        foreach (var field in EnumerateFields(request))
        {
            if (string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            if (!QuestionAuthoringContentSupport.TrySegment(field.Value, out var segments, out var errorCode, out var errorMessage))
            {
                details.Add(new LatexValidationDetailDto(
                    field.Path,
                    false,
                    null,
                    errorCode,
                    errorMessage,
                    QuestionAuthoringContentSupport.BuildSafeFallbackText(field.Value)));
                continue;
            }

            var fieldValid = true;
            string? lastErrorCode = null;
            string? lastErrorMessage = null;
            var builder = new List<ContentSegmentDto>();
            foreach (var segment in segments)
            {
                if (segment.Kind == AuthoringSegmentKind.Text)
                {
                    builder.Add(new ContentSegmentDto("text", QuestionAuthoringContentSupport.NormalizeText(segment.Value)));
                    continue;
                }

                var validation = QuestionAuthoringContentSupport.ValidateMathSegment(segment.Value);
                builder.Add(new ContentSegmentDto(segment.KindName, validation.NormalizedExpression ?? segment.Value));
                if (!validation.IsValid)
                {
                    fieldValid = false;
                    lastErrorCode = validation.ErrorCode;
                    lastErrorMessage = validation.ErrorMessage;
                    break;
                }
            }

            var normalizedValue = fieldValid
                ? BuildNormalizedField(segments, builder)
                : null;

            details.Add(new LatexValidationDetailDto(
                field.Path,
                fieldValid,
                normalizedValue,
                lastErrorCode,
                lastErrorMessage,
                fieldValid ? QuestionAuthoringContentSupport.BuildSafeFallbackText(normalizedValue) : QuestionAuthoringContentSupport.BuildSafeFallbackText(field.Value)));
        }

        return new LatexValidationResultDto(details.All(x => x.IsValid), details);
    }

    private static string BuildNormalizedField(
        IReadOnlyList<AuthoringContentSegment> segments,
        IReadOnlyList<ContentSegmentDto> normalizedSegments)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var normalized = normalizedSegments[i];
            if (segment.Kind == AuthoringSegmentKind.Text)
            {
                builder.Append(normalized.Value);
            }
            else
            {
                builder.Append(segment.Kind == AuthoringSegmentKind.DisplayMath ? "$$" : "$");
                builder.Append(normalized.Value);
                builder.Append(segment.Kind == AuthoringSegmentKind.DisplayMath ? "$$" : "$");
            }
        }

        return builder.ToString().Trim();
    }

    private static IEnumerable<(string Path, string? Value)> EnumerateFields(QuestionAuthoringRequest request)
    {
        yield return ("text", request.Text);
        yield return ("correctAnswer", request.CorrectAnswer);
        yield return ("explanation", request.Explanation);

        for (var index = 0; index < request.Options.Count; index++)
        {
            yield return ($"options[{index}].text", request.Options[index].Text);
        }

        for (var index = 0; index < request.Hints.Count; index++)
        {
            yield return ($"hints[{index}].text", request.Hints[index].Text);
        }

        for (var index = 0; index < request.Steps.Count; index++)
        {
            yield return ($"steps[{index}].text", request.Steps[index].Text);
            yield return ($"steps[{index}].hint", request.Steps[index].Hint);
        }
    }
}

public sealed class MathNormalizationService : IMathNormalizationService
{
    public QuestionNormalizationResultDto Normalize(QuestionAuthoringRequest request)
    {
        var fields = new List<NormalizedContentFieldDto>();
        foreach (var field in EnumerateFields(request))
        {
            if (field.Value is null)
            {
                continue;
            }

            var normalized = QuestionAuthoringContentSupport.NormalizeMixedContent(field.Value, out var segments);
            fields.Add(new NormalizedContentFieldDto(field.Path, field.Value, normalized, segments));
        }

        return new QuestionNormalizationResultDto(true, fields);
    }

    private static IEnumerable<(string Path, string Value)> EnumerateFields(QuestionAuthoringRequest request)
    {
        yield return ("text", request.Text);

        if (!string.IsNullOrWhiteSpace(request.CorrectAnswer))
        {
            yield return ("correctAnswer", request.CorrectAnswer!);
        }

        if (!string.IsNullOrWhiteSpace(request.Explanation))
        {
            yield return ("explanation", request.Explanation!);
        }

        for (var index = 0; index < request.Options.Count; index++)
        {
            yield return ($"options[{index}].text", request.Options[index].Text);
        }

        for (var index = 0; index < request.Hints.Count; index++)
        {
            yield return ($"hints[{index}].text", request.Hints[index].Text);
        }

        for (var index = 0; index < request.Steps.Count; index++)
        {
            yield return ($"steps[{index}].text", request.Steps[index].Text);
            if (!string.IsNullOrWhiteSpace(request.Steps[index].Hint))
            {
                yield return ($"steps[{index}].hint", request.Steps[index].Hint!);
            }
        }
    }
}

public sealed class MathEquivalenceService : IMathEquivalenceService
{
    public Task<EquivalentAnswerResultDto> AreEquivalentAsync(
        string expected,
        string actual,
        MathEquivalenceContext context,
        CancellationToken cancellationToken)
    {
        var normalizedExpected = QuestionAuthoringContentSupport.ConvertForEvaluation(expected);
        var normalizedActual = QuestionAuthoringContentSupport.ConvertForEvaluation(actual);

        if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedActual))
        {
            return Task.FromResult(new EquivalentAnswerResultDto(false, expected, actual, "invalid", "One of the expressions is empty."));
        }

        if (string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
        {
            return Task.FromResult(new EquivalentAnswerResultDto(true, expected, actual, "canonical", "Canonical expressions match."));
        }

        var expectedIdentifiers = QuestionAuthoringContentSupport.ExtractIdentifiers(normalizedExpected);
        var actualIdentifiers = QuestionAuthoringContentSupport.ExtractIdentifiers(normalizedActual);
        var identifiers = expectedIdentifiers.Union(actualIdentifiers, StringComparer.Ordinal).ToArray();

        if (identifiers.Length == 0)
        {
            if (!MathExpressionEngine.TryEvaluate(normalizedExpected, new Dictionary<string, double>(), out var expectedValue, out var expectedError))
            {
                return Task.FromResult(new EquivalentAnswerResultDto(false, expected, actual, "numeric", expectedError));
            }

            if (!MathExpressionEngine.TryEvaluate(normalizedActual, new Dictionary<string, double>(), out var actualValue, out var actualError))
            {
                return Task.FromResult(new EquivalentAnswerResultDto(false, expected, actual, "numeric", actualError));
            }

            var equivalent = Math.Abs(expectedValue - actualValue) <= context.NumericTolerance;
            return Task.FromResult(new EquivalentAnswerResultDto(
                equivalent,
                expected,
                actual,
                "numeric",
                equivalent ? "Numeric values match within tolerance." : $"Numeric values differ ({expectedValue} vs {actualValue})."));
        }

        if (!context.AllowSymbolicComparison)
        {
            return Task.FromResult(new EquivalentAnswerResultDto(false, expected, actual, "symbolic", "Symbolic comparison is disabled."));
        }

        var samples = new[] { -3d, -2d, -1d, -0.5d, 0d, 0.5d, 1d, 2d, 3d };
        var comparableSamples = 0;
        foreach (var sampleIndex in Enumerable.Range(0, samples.Length))
        {
            var variableMap = identifiers
                .Select((name, index) => new KeyValuePair<string, double>(name, samples[(sampleIndex + index) % samples.Length]))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            var expectedOk = MathExpressionEngine.TryEvaluate(normalizedExpected, variableMap, out var expectedValue, out _);
            var actualOk = MathExpressionEngine.TryEvaluate(normalizedActual, variableMap, out var actualValue, out _);

            if (!expectedOk && !actualOk)
            {
                continue;
            }

            if (!expectedOk || !actualOk)
            {
                return Task.FromResult(new EquivalentAnswerResultDto(false, expected, actual, "symbolic", "Expressions diverge on a sampled variable assignment."));
            }

            comparableSamples++;
            if (Math.Abs(expectedValue - actualValue) > context.NumericTolerance)
            {
                return Task.FromResult(new EquivalentAnswerResultDto(
                    false,
                    expected,
                    actual,
                    "symbolic",
                    $"Expressions differ for sample {JsonSerializer.Serialize(variableMap)}."));
            }
        }

        return Task.FromResult(new EquivalentAnswerResultDto(
            comparableSamples > 0,
            expected,
            actual,
            "symbolic",
            comparableSamples > 0 ? "Expressions matched across sampled assignments." : "Expressions could not be compared on any valid sample."));
    }

    public Task<string?> NormalizeAnswerAsync(string expression, CancellationToken cancellationToken)
        => Task.FromResult<string?>(QuestionAuthoringContentSupport.NormalizeMathExpression(expression));

    public Task<MathExpressionValidationResultDto> ValidateExpressionAsync(string expression, CancellationToken cancellationToken)
    {
        var normalized = QuestionAuthoringContentSupport.ConvertForEvaluation(expression);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Task.FromResult(new MathExpressionValidationResultDto(false, expression, null, "math.empty_expression", "Expression is empty."));
        }

        var identifiers = QuestionAuthoringContentSupport.ExtractIdentifiers(normalized)
            .ToDictionary(name => name, _ => 2d, StringComparer.Ordinal);

        var ok = MathExpressionEngine.TryEvaluate(normalized, identifiers, out _, out var error);
        return Task.FromResult(new MathExpressionValidationResultDto(ok, expression, normalized, ok ? null : "math.invalid_expression", error));
    }
}

public sealed class StepExplanationValidationService : IStepExplanationValidationService
{
    public Task<IReadOnlyList<StepValidationResultDto>> ValidateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken)
    {
        var orderedSteps = request.Steps.OrderBy(x => x.Order).ToList();
        var results = new List<StepValidationResultDto>(orderedSteps.Count);

        StepExplanationAuthoringDto? previous = null;
        var expectedOrder = 1;

        foreach (var step in orderedSteps)
        {
            var issues = new List<ValidationIssueDto>();
            if (string.IsNullOrWhiteSpace(step.Text))
            {
                issues.Add(new ValidationIssueDto(
                    ValidationStageNames.Steps,
                    ValidationIssueSeverities.Error,
                    "steps.empty_text",
                    "Step explanation text must not be empty.",
                    $"steps[{step.Order}].text"));
            }

            if (step.Order != expectedOrder)
            {
                issues.Add(new ValidationIssueDto(
                    ValidationStageNames.Steps,
                    ValidationIssueSeverities.Error,
                    "steps.invalid_order",
                    $"Expected step order {expectedOrder} but received {step.Order}.",
                    $"steps[{step.Order}].order"));
                expectedOrder = step.Order;
            }

            expectedOrder++;

            ValidateStepField(step.Text, $"steps[{step.Order}].text", issues);
            ValidateStepField(step.Hint, $"steps[{step.Order}].hint", issues);

            var followsPrevious = previous is null || StepFollows(previous.Text, step.Text);
            if (previous is not null && !followsPrevious)
            {
                issues.Add(new ValidationIssueDto(
                    ValidationStageNames.Steps,
                    ValidationIssueSeverities.Warning,
                    "steps.logical_transition_unclear",
                    "This step does not clearly follow from the previous step.",
                    $"steps[{step.Order}]",
                    "Check whether the transformation or wording is internally consistent."));
            }

            results.Add(new StepValidationResultDto(
                step.Order,
                issues.All(x => !string.Equals(x.Severity, ValidationIssueSeverities.Error, StringComparison.Ordinal)),
                followsPrevious,
                issues));

            previous = step;
        }

        return Task.FromResult<IReadOnlyList<StepValidationResultDto>>(results);
    }

    private static void ValidateStepField(string? value, string fieldPath, List<ValidationIssueDto> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (QuestionAuthoringContentSupport.ContainsUnsafeMarkup(value))
        {
            issues.Add(new ValidationIssueDto(
                ValidationStageNames.Steps,
                ValidationIssueSeverities.Error,
                "steps.unsafe_markup",
                "Step content contains unsafe markup.",
                fieldPath));
        }

        if (!QuestionAuthoringContentSupport.TrySegment(value, out var segments, out var errorCode, out var errorMessage))
        {
            issues.Add(new ValidationIssueDto(
                ValidationStageNames.Steps,
                ValidationIssueSeverities.Error,
                errorCode ?? "steps.invalid_math_delimiter",
                errorMessage ?? "Step content contains invalid math delimiters.",
                fieldPath));
            return;
        }

        foreach (var segment in segments.Where(x => x.Kind != AuthoringSegmentKind.Text))
        {
            var latexResult = QuestionAuthoringContentSupport.ValidateMathSegment(segment.Value);
            if (!latexResult.IsValid)
            {
                issues.Add(new ValidationIssueDto(
                    ValidationStageNames.Steps,
                    ValidationIssueSeverities.Error,
                    latexResult.ErrorCode ?? "steps.invalid_latex",
                    latexResult.ErrorMessage ?? "Step contains invalid LaTeX.",
                    fieldPath));
            }
        }
    }

    private static bool StepFollows(string previousText, string currentText)
    {
        var previousNormalized = QuestionAuthoringContentSupport.ConvertForEvaluation(previousText);
        var currentNormalized = QuestionAuthoringContentSupport.ConvertForEvaluation(currentText);
        if (string.Equals(previousNormalized, currentNormalized, StringComparison.Ordinal))
        {
            return false;
        }

        var previousIdentifiers = QuestionAuthoringContentSupport.ExtractIdentifiers(previousNormalized);
        var currentIdentifiers = QuestionAuthoringContentSupport.ExtractIdentifiers(currentNormalized);

        if (previousIdentifiers.Count == 0 && currentIdentifiers.Count == 0)
        {
            return previousText.Contains('=') == currentText.Contains('=');
        }

        return previousIdentifiers.Overlaps(currentIdentifiers);
    }
}

public sealed class DifficultyEstimationService : IDifficultyEstimationService
{
    public DifficultyEstimateResultDto Estimate(QuestionAuthoringRequest request, QuestionNormalizationResultDto normalizationResult)
    {
        var signals = new List<string>();
        var score = 0;
        var combinedContent = string.Join(' ', normalizationResult.Fields.Select(x => x.NormalizedValue));

        var operatorCount = combinedContent.Count(x => x is '+' or '-' or '*' or '/' or '=' or '^');
        if (operatorCount > 0)
        {
            score += Math.Min(20, operatorCount * 2);
            signals.Add($"operators:{operatorCount}");
        }

        var variableCount = QuestionAuthoringContentSupport.ExtractIdentifiers(QuestionAuthoringContentSupport.ConvertForEvaluation(combinedContent)).Count;
        if (variableCount > 0)
        {
            score += Math.Min(15, variableCount * 5);
            signals.Add($"variables:{variableCount}");
        }

        if (combinedContent.Contains(@"\frac", StringComparison.Ordinal) || combinedContent.Contains('/'))
        {
            score += 12;
            signals.Add("fractions");
        }

        if (combinedContent.Contains(@"\sqrt", StringComparison.Ordinal) || combinedContent.Contains("sqrt", StringComparison.OrdinalIgnoreCase))
        {
            score += 14;
            signals.Add("roots");
        }

        if (request.Steps.Count > 0)
        {
            score += Math.Min(20, request.Steps.Count * 4);
            signals.Add($"steps:{request.Steps.Count}");
        }

        if (request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 20)
        {
            score += 10;
            signals.Add("word_problem_density");
        }

        if (string.Equals(request.Type, "open_answer", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
            signals.Add("open_answer");
        }

        score = Math.Clamp(score, 1, 100);
        var band = score switch
        {
            <= 25 => "easy",
            <= 50 => "medium",
            <= 75 => "hard",
            _ => "expert"
        };

        return new DifficultyEstimateResultDto(score, band, signals);
    }
}

public sealed class QuestionPreviewService : IQuestionPreviewService
{
    public QuestionPreviewPayloadDto BuildPreview(
        QuestionAuthoringRequest request,
        ContentLintResultDto lint,
        LatexValidationResultDto latex,
        QuestionNormalizationResultDto normalized,
        IReadOnlyList<EquivalentAnswerResultDto> equivalenceChecks,
        IReadOnlyList<StepValidationResultDto> stepResults,
        DifficultyEstimateResultDto difficulty,
        QuestionValidationSummaryDto summary)
    {
        var safePreviewFields = normalized.Fields
            .ToDictionary(
                x => x.FieldPath,
                x => (string?)(latex.Fields.FirstOrDefault(f => f.FieldPath == x.FieldPath)?.SafeFallbackText ?? x.NormalizedValue),
                StringComparer.Ordinal);

        return new QuestionPreviewPayloadDto(
            request,
            normalized,
            latex,
            lint,
            equivalenceChecks,
            stepResults,
            difficulty,
            summary,
            safePreviewFields);
    }
}

public sealed class QuestionPublishGuardService : IQuestionPublishGuardService
{
    public QuestionValidationSummaryDto BuildSummary(
        ContentLintResultDto lint,
        LatexValidationResultDto latex,
        IReadOnlyList<EquivalentAnswerResultDto> equivalenceChecks,
        IReadOnlyList<StepValidationResultDto> stepResults)
    {
        var issues = new List<ValidationIssueDto>();
        issues.AddRange(lint.Issues);

        foreach (var field in latex.Fields.Where(x => !x.IsValid))
        {
            issues.Add(new ValidationIssueDto(
                ValidationStageNames.Latex,
                ValidationIssueSeverities.Error,
                field.ErrorCode ?? "latex.invalid",
                field.ErrorMessage ?? "Invalid LaTeX expression.",
                field.FieldPath,
                "Fix the invalid LaTeX before publishing."));
        }

        foreach (var result in equivalenceChecks.Where(x => !x.IsEquivalent))
        {
            issues.Add(new ValidationIssueDto(
                ValidationStageNames.Equivalence,
                ValidationIssueSeverities.Error,
                "equivalence.mismatch",
                result.Reason ?? "Expected answer does not match the configured correct option.",
                "correctAnswer",
                "Align the explicit correct answer and the correct option text."));
        }

        foreach (var stepIssue in stepResults.SelectMany(x => x.Issues))
        {
            issues.Add(stepIssue);
        }

        var errorCount = issues.Count(x => string.Equals(x.Severity, ValidationIssueSeverities.Error, StringComparison.Ordinal));
        var warningCount = issues.Count(x => string.Equals(x.Severity, ValidationIssueSeverities.Warning, StringComparison.Ordinal));
        var status = errorCount > 0
            ? QuestionValidationStatuses.Failed
            : warningCount > 0
                ? QuestionValidationStatuses.PassedWithWarnings
                : QuestionValidationStatuses.Passed;

        return new QuestionValidationSummaryDto(errorCount == 0, status, errorCount, warningCount, issues);
    }
}

public sealed class NoOpQuestionAutoHintGenerator : IQuestionAutoHintGenerator
{
    private readonly ILogger<NoOpQuestionAutoHintGenerator> logger;

    public NoOpQuestionAutoHintGenerator(ILogger<NoOpQuestionAutoHintGenerator> logger)
    {
        this.logger = logger;
    }

    public Task<IReadOnlyList<QuestionHintDto>> GenerateAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Auto-hint generation hook invoked for question {QuestionId}.", request.QuestionId);
        return Task.FromResult<IReadOnlyList<QuestionHintDto>>(request.Hints);
    }
}
