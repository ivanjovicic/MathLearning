using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Enums;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

internal static class QuestionAuthoringRequestSanitizer
{
    public static QuestionAuthoringRequest Sanitize(
        QuestionAuthoringRequest request,
        IMathContentSanitizer sanitizer)
    {
        var sanitizedHints = request.Hints
            .Select(hint => hint with
            {
                Text = sanitizer.NormalizeMathContent(hint.Text, request.HintFormat),
                SemanticsAltText = string.IsNullOrWhiteSpace(hint.SemanticsAltText)
                    ? sanitizer.GenerateSemanticsAltText(hint.Text, request.HintFormat)
                    : sanitizer.NormalizeMathContent(hint.SemanticsAltText, ContentFormat.PlainText)
            })
            .ToArray();

        var sanitizedOptions = request.Options
            .Select(option => option with
            {
                Text = sanitizer.NormalizeMathContent(option.Text, option.TextFormat),
                SemanticsAltText = string.IsNullOrWhiteSpace(option.SemanticsAltText)
                    ? sanitizer.GenerateSemanticsAltText(option.Text, option.TextFormat)
                    : sanitizer.NormalizeMathContent(option.SemanticsAltText, ContentFormat.PlainText)
            })
            .ToArray();

        var sanitizedSteps = request.Steps
            .Select(step => step with
            {
                Text = sanitizer.NormalizeMathContent(step.Text, step.TextFormat),
                Hint = sanitizer.NormalizeMathContent(step.Hint, step.HintFormat),
                SemanticsAltText = string.IsNullOrWhiteSpace(step.SemanticsAltText)
                    ? sanitizer.GenerateSemanticsAltText(step.Text, step.TextFormat)
                    : sanitizer.NormalizeMathContent(step.SemanticsAltText, ContentFormat.PlainText)
            })
            .ToArray();

        return request with
        {
            Text = sanitizer.NormalizeMathContent(request.Text, request.TextFormat),
            CorrectAnswer = sanitizer.NormalizeMathContent(request.CorrectAnswer, request.TextFormat),
            Explanation = sanitizer.NormalizeMathContent(request.Explanation, request.ExplanationFormat),
            Hints = sanitizedHints,
            Options = sanitizedOptions,
            Steps = sanitizedSteps,
            SemanticsAltText = string.IsNullOrWhiteSpace(request.SemanticsAltText)
                ? sanitizer.GenerateSemanticsAltText(request.Text, request.TextFormat)
                : sanitizer.NormalizeMathContent(request.SemanticsAltText, ContentFormat.PlainText)
        };
    }
}
