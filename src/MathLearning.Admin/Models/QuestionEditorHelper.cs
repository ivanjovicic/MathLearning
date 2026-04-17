using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Entities;
using System.Text.Json;

namespace MathLearning.Admin.Models;

/// <summary>
/// Helper class for question editor shared logic (New/Edit)
/// </summary>
public class QuestionEditorHelper
{
    private readonly IMathContentSanitizer _sanitizer;

    public QuestionEditorHelper(IMathContentSanitizer sanitizer)
    {
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    /// <summary>
    /// Validate question editor model
    /// </summary>
    public List<string> ValidateModel(QuestionEditorModel model)
        => QuestionEditorValidation.Validate(model);

    public QuestionAuthoringRequest ToAuthoringRequest(QuestionEditorModel model, int? questionId = null)
    {
        var isMultipleChoice = string.Equals(model.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase);
        var options = isMultipleChoice
            ? model.Options
                .Select(x => new QuestionAuthoringOptionDto(
                    x.Id,
                    x.Text ?? string.Empty,
                    x.IsCorrect,
                    x.TextFormat,
                    x.RenderMode,
                    x.SemanticsAltText))
                .ToArray()
            : Array.Empty<QuestionAuthoringOptionDto>();

        var hints = new List<QuestionHintDto>();
        if (!string.IsNullOrWhiteSpace(model.HintFormula))
        {
            hints.Add(new QuestionHintDto("formula", model.HintFormula, null));
        }

        if (!string.IsNullOrWhiteSpace(model.HintClue))
        {
            hints.Add(new QuestionHintDto("clue", model.HintClue, null));
        }

        if (!string.IsNullOrWhiteSpace(model.HintFull))
        {
            hints.Add(new QuestionHintDto("full", model.HintFull, null));
        }

        var steps = model.Steps
            .OrderBy(x => x.Order)
            .Select(x => new StepExplanationAuthoringDto(
                x.Order,
                x.Text ?? string.Empty,
                x.Hint,
                x.Highlight,
                x.TextFormat,
                x.HintFormat,
                x.TextRenderMode,
                x.HintRenderMode,
                x.SemanticsAltText))
            .ToArray();

        var selectedOption = model.Options.FirstOrDefault(x => x.IsCorrect);
        var correctAnswer = isMultipleChoice
            ? selectedOption?.Text
            : model.CorrectAnswer;

        return new QuestionAuthoringRequest(
            questionId,
            model.Text ?? string.Empty,
            model.Type,
            correctAnswer,
            model.Explanation,
            model.Difficulty,
            model.CategoryId,
            model.SubtopicId,
            options,
            hints,
            steps,
            null,
            model.Steps.Count > 0,
            model.TextFormat,
            model.ExplanationFormat,
            model.HintFormat,
            model.TextRenderMode,
            model.ExplanationRenderMode,
            model.HintRenderMode,
            model.SemanticsAltText,
            isMultipleChoice ? selectedOption?.Id : null);
    }

    /// <summary>
    /// Sanitize content based on format
    /// </summary>
    public string Sanitize(string? raw, MathLearning.Domain.Enums.ContentFormat format)
        => _sanitizer.NormalizeMathContent(raw, format);

    /// <summary>
    /// Resolve semantics alt text or generate from content
    /// </summary>
    public string? ResolveSemantics(string? semanticsAltText, string? raw, MathLearning.Domain.Enums.ContentFormat format)
        => !string.IsNullOrWhiteSpace(semanticsAltText)
            ? semanticsAltText.Trim()
            : _sanitizer.GenerateSemanticsAltText(raw, format);

    /// <summary>
    /// Build question options from editor model
    /// </summary>
    public List<QuestionOption> BuildOptions(QuestionEditorModel model)
        => model.Options
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select((x, i) => new QuestionOption(
                Sanitize(x.Text, x.TextFormat),
                x.IsCorrect,
                x.TextFormat,
                x.RenderMode,
                ResolveSemantics(x.SemanticsAltText, x.Text, x.TextFormat),
                i + 1))
            .ToList();

    /// <summary>
    /// Build question steps from editor model
    /// </summary>
    public List<QuestionStep> BuildSteps(int questionId, QuestionEditorModel model)
        => model.Steps
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .OrderBy(x => x.Order)
            .Select(x => new QuestionStep(
                questionId,
                x.Order,
                Sanitize(x.Text, x.TextFormat),
                Sanitize(x.Hint, x.HintFormat),
                x.Highlight,
                x.TextFormat,
                x.HintFormat,
                x.TextRenderMode,
                x.HintRenderMode,
                ResolveSemantics(x.SemanticsAltText, x.Text, x.TextFormat)))
            .ToList();

    /// <summary>
    /// Apply question properties from editor model to question entity
    /// </summary>
    public void ApplyQuestionFields(Question question, QuestionEditorModel model)
    {
        question.SetType(model.Type);
        question.SetCategory(model.CategoryId);
        question.SetSubtopic(model.SubtopicId);
        question.SetDifficulty(model.Difficulty);
        question.SetExplanation(Sanitize(model.Explanation, model.ExplanationFormat));
        question.SetHintFormula(Sanitize(model.HintFormula, model.HintFormat));
        question.SetHintClue(Sanitize(model.HintClue, model.HintFormat));
        question.SetHintFull(Sanitize(model.HintFull, model.HintFormat));
        question.SetTextFormat(model.TextFormat);
        question.SetExplanationFormat(model.ExplanationFormat);
        question.SetHintFormat(model.HintFormat);
        question.SetTextRenderMode(model.TextRenderMode);
        question.SetExplanationRenderMode(model.ExplanationRenderMode);
        question.SetHintRenderMode(model.HintRenderMode);
        question.SetSemanticsAltText(ResolveSemantics(model.SemanticsAltText, model.Text, model.TextFormat));
        question.SetHintDifficulty(model.Difficulty switch
        {
            <= 2 => 1,
            3 or 4 => 2,
            _ => 3
        });
    }

    /// <summary>
    /// Apply stable answer mapping after options are persisted.
    /// </summary>
    public void ApplyAnswerMapping(Question question, QuestionEditorModel model)
    {
        if (!string.Equals(model.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            question.SetCorrectOptionId(null);
            question.SetCorrectAnswer(Sanitize(model.CorrectAnswer, model.TextFormat));
            question.EnsureAnswerInvariant();
            return;
        }

        question.SyncCorrectOptionFromOptions();
        if (question.CorrectOptionId is null)
        {
            var resolved = ResolvePersistedCorrectOption(question, model);
            question.SetCorrectOptionId(resolved?.Id);
            if (resolved is not null)
            {
                question.SetCorrectAnswer(resolved.Text);
            }
        }

        question.EnsureAnswerInvariant();
    }

    /// <summary>
    /// Create snapshot of model for change detection
    /// </summary>
    public string CreateModelSnapshot(QuestionEditorModel model)
        => JsonSerializer.Serialize(model);

    /// <summary>
    /// Map question entity to editor model
    /// </summary>
    public void MapQuestionToModel(Question question, QuestionEditorModel model)
    {
        model.Type = question.Type;
        model.Text = question.Text;
        model.TextFormat = question.TextFormat;
        model.TextRenderMode = question.TextRenderMode;
        model.SemanticsAltText = question.SemanticsAltText;
        model.Explanation = question.Explanation;
        model.ExplanationFormat = question.ExplanationFormat;
        model.ExplanationRenderMode = question.ExplanationRenderMode;
        model.HintFormula = question.HintFormula;
        model.HintClue = question.HintClue;
        model.HintFull = question.HintFull;
        model.HintFormat = question.HintFormat;
        model.HintRenderMode = question.HintRenderMode;
        model.CorrectAnswer = question.CorrectAnswer;
        model.CategoryId = question.CategoryId;
        model.SubtopicId = question.SubtopicId;
        model.Difficulty = question.Difficulty;

        var resolvedCorrectOptionId = question.CorrectOptionId;
        if (!resolvedCorrectOptionId.HasValue && !string.IsNullOrWhiteSpace(question.CorrectAnswer))
        {
            resolvedCorrectOptionId = question.Options
                .OrderBy(x => x.Order)
                .FirstOrDefault(x => string.Equals(x.Text, question.CorrectAnswer, StringComparison.Ordinal))?.Id;
        }
        if (!resolvedCorrectOptionId.HasValue)
        {
            resolvedCorrectOptionId = question.Options
                .OrderBy(x => x.Order)
                .FirstOrDefault(x => x.IsCorrect)?.Id;
        }

        model.Options = question.Options
            .OrderBy(x => x.Order)
            .Select(x => new QuestionOptionEditorModel
            {
                Id = x.Id,
                Text = x.Text,
                TextFormat = x.TextFormat,
                RenderMode = x.RenderMode,
                SemanticsAltText = x.SemanticsAltText,
                IsCorrect = resolvedCorrectOptionId.HasValue && x.Id == resolvedCorrectOptionId.Value
            })
            .ToList();
        model.Steps = question.Steps
            .OrderBy(x => x.StepIndex)
            .Select(x => new QuestionStepEditorModel
            {
                Id = x.Id,
                Order = x.StepIndex,
                Text = x.Text,
                TextFormat = x.TextFormat,
                TextRenderMode = x.TextRenderMode,
                Hint = x.Hint,
                HintFormat = x.HintFormat,
                HintRenderMode = x.HintRenderMode,
                SemanticsAltText = x.SemanticsAltText,
                Highlight = x.Highlight
            })
            .ToList();

        if (model.Options.Count == 0)
        {
            model.Options = QuestionEditorModel.CreateDefaultOptions();
            model.Options[0].IsCorrect = true;
        }
    }

    private QuestionOption? ResolvePersistedCorrectOption(Question question, QuestionEditorModel model)
    {
        var orderedPersistedOptions = question.Options
            .OrderBy(x => x.Order)
            .ToList();

        var byFlag = orderedPersistedOptions.FirstOrDefault(x => x.IsCorrect);
        if (byFlag is not null)
        {
            return byFlag;
        }

        var selectedModelOption = model.Options.FirstOrDefault(x => x.IsCorrect);
        if (selectedModelOption?.Id is int selectedId)
        {
            var byId = orderedPersistedOptions.FirstOrDefault(x => x.Id == selectedId);
            if (byId is not null)
            {
                return byId;
            }
        }

        var selectedFilledIndex = model.Options
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select((x, i) => new { Option = x, Index = i })
            .FirstOrDefault(x => x.Option.IsCorrect)?.Index;

        if (selectedFilledIndex.HasValue && selectedFilledIndex.Value < orderedPersistedOptions.Count)
        {
            return orderedPersistedOptions[selectedFilledIndex.Value];
        }

        if (!string.IsNullOrWhiteSpace(question.CorrectAnswer))
        {
            return orderedPersistedOptions.FirstOrDefault(x =>
                string.Equals(x.Text, question.CorrectAnswer, StringComparison.Ordinal));
        }

        return null;
    }
}
