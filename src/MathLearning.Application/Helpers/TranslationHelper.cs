using MathLearning.Domain.Entities;

using MathLearning.Application.Content;
using MathLearning.Domain.Enums;

namespace MathLearning.Application.Helpers;

public static class TranslationHelper
{
    private static readonly IMathContentSanitizer Sanitizer = new MathContentSanitizer();

    /// <summary>
    /// Default language for question content (original text stored on Question entity).
    /// </summary>
    public const string DefaultLang = "sr";

    /// <summary>
    /// Fallback language when user's preferred language is not available.
    /// </summary>
    public const string FallbackLang = "en";

    /// <summary>
    /// Returns translated text for a question, falling back to: userLang → en → original Question.Text
    /// </summary>
    public static string GetText(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.Text;

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation != null)
            return translation.Text;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation != null)
                return translation.Text;
        }

        return question.Text;
    }

    /// <summary>
    /// Returns translated explanation, falling back to: userLang → en → original Question.Explanation
    /// </summary>
    public static string? GetExplanation(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.Explanation;

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.Explanation != null)
            return translation.Explanation;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.Explanation != null)
                return translation.Explanation;
        }

        return question.Explanation;
    }

    /// <summary>
    /// Returns translated hint formula.
    /// </summary>
    public static string? GetHintFormula(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.HintFormula;

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.HintFormula != null)
            return translation.HintFormula;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.HintFormula != null)
                return translation.HintFormula;
        }

        return question.HintFormula;
    }

    /// <summary>
    /// Returns translated hint clue.
    /// </summary>
    public static string? GetHintClue(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.HintClue;

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.HintClue != null)
            return translation.HintClue;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.HintClue != null)
                return translation.HintClue;
        }

        return question.HintClue;
    }

    /// <summary>
    /// Returns translated option text, falling back to: userLang → en → original Option.Text
    /// </summary>
    public static string GetOptionText(QuestionOption option, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return option.Text;

        var translation = option.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation != null)
            return translation.Text;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = option.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation != null)
                return translation.Text;
        }

        return option.Text;
    }

    /// <summary>
    /// Resolves user language from settings value or Accept-Language header value.
    /// </summary>
    public static string ResolveLanguage(string? settingsLang, string? acceptLanguageHeader)
    {
        // 1. UserSettings.Language (if loaded)
        if (!string.IsNullOrWhiteSpace(settingsLang))
            return settingsLang.Trim().ToLowerInvariant();

        // 2. Accept-Language header
        if (!string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            var primaryLang = acceptLanguageHeader.Split(',', ';')[0].Trim().ToLowerInvariant();
            if (primaryLang.Contains('-'))
                primaryLang = primaryLang.Split('-')[0];
            return primaryLang;
        }

        return DefaultLang;
    }

    /// <summary>
    /// Returns translated hint light.
    /// </summary>
    public static string? GetHintLight(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.HintFormula; // Fallback to original hint

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.HintLight != null)
            return translation.HintLight;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.HintLight != null)
                return translation.HintLight;
        }

        return question.HintFormula; // Fallback to original hint
    }

    /// <summary>
    /// Returns translated hint medium.
    /// </summary>
    public static string? GetHintMedium(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.HintClue; // Fallback to original hint

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.HintMedium != null)
            return translation.HintMedium;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.HintMedium != null)
                return translation.HintMedium;
        }

        return question.HintClue; // Fallback to original hint
    }

    /// <summary>
    /// Returns translated hint full.
    /// </summary>
    public static string? GetHintFull(Question question, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return question.HintFull;

        var translation = question.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));

        if (translation?.HintFull != null)
            return translation.HintFull;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = question.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));

            if (translation?.HintFull != null)
                return translation.HintFull;
        }

        return question.HintFull;
    }

    public static string? ResolveSemanticsAltText(string? semanticsAltText, string? raw, ContentFormat format)
        => string.IsNullOrWhiteSpace(semanticsAltText)
            ? Sanitizer.GenerateSemanticsAltText(raw, format)
            : semanticsAltText;

    public static string? GetQuestionSemanticsAltText(Question question, string userLang)
        => ResolveSemanticsAltText(question.SemanticsAltText, GetText(question, userLang), question.TextFormat);

    public static string? GetOptionSemanticsAltText(QuestionOption option, string userLang)
        => ResolveSemanticsAltText(option.SemanticsAltText, GetOptionText(option, userLang), option.TextFormat);

    public static string? GetStepSemanticsAltText(QuestionStep step, string userLang)
        => ResolveSemanticsAltText(step.SemanticsAltText, GetTranslatedStepText(step, userLang), step.TextFormat);

    private static string GetTranslatedStepText(QuestionStep step, string userLang)
    {
        if (string.Equals(userLang, DefaultLang, StringComparison.OrdinalIgnoreCase))
            return step.Text;

        var translation = step.Translations
            .FirstOrDefault(t => string.Equals(t.Lang, userLang, StringComparison.OrdinalIgnoreCase));
        if (translation != null)
            return translation.Text;

        if (!string.Equals(userLang, FallbackLang, StringComparison.OrdinalIgnoreCase))
        {
            translation = step.Translations
                .FirstOrDefault(t => string.Equals(t.Lang, FallbackLang, StringComparison.OrdinalIgnoreCase));
            if (translation != null)
                return translation.Text;
        }

        return step.Text;
    }
}
