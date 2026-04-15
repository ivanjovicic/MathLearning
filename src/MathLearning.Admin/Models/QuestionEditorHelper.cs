using MathLearning.Application.Content;
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
    /// Resolve correct answer based on question type
    /// </summary>
    public string? ResolveCorrectAnswer(QuestionEditorModel model)
    {
        if (!string.Equals(model.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            return Sanitize(model.CorrectAnswer, model.TextFormat);
        }

        var correct = model.Options.FirstOrDefault(x => x.IsCorrect)?.Text;
        return Sanitize(correct, model.TextFormat);
    }

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
        question.SetCorrectAnswer(ResolveCorrectAnswer(model));
        question.SetHintDifficulty(model.Difficulty switch
        {
            <= 2 => 1,
            3 or 4 => 2,
            _ => 3
        });
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
        model.Options = question.Options
            .OrderBy(x => x.Order)
            .Select(x => new QuestionOptionEditorModel
            {
                Id = x.Id,
                Text = x.Text,
                TextFormat = x.TextFormat,
                RenderMode = x.RenderMode,
                SemanticsAltText = x.SemanticsAltText,
                IsCorrect = x.IsCorrect
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
}
