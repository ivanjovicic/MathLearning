using MathLearning.Application.Helpers;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Enums;

namespace MathLearning.Tests.Helpers;

public sealed class TranslationHelperTests
{
    [Fact]
    public void GetText_DefaultLanguageAlwaysUsesOriginalQuestionText()
    {
        var question = CreateTranslatedQuestion();

        var text = TranslationHelper.GetText(question, "SR");

        Assert.Equal("Originalno pitanje", text);
    }

    [Fact]
    public void GetText_UsesRequestedTranslationThenEnglishFallbackThenOriginal()
    {
        var question = CreateTranslatedQuestion();

        Assert.Equal("Deutsche Frage", TranslationHelper.GetText(question, "de"));
        Assert.Equal("English question", TranslationHelper.GetText(question, "es"));

        question.ReplaceTranslations(Array.Empty<QuestionTranslation>());
        Assert.Equal("Originalno pitanje", TranslationHelper.GetText(question, "es"));
    }

    [Fact]
    public void GetExplanation_UsesRequestedTranslationThenEnglishFallbackThenOriginal()
    {
        var question = CreateTranslatedQuestion();

        Assert.Equal("Deutsche Erklärung", TranslationHelper.GetExplanation(question, "de"));
        Assert.Equal("English explanation", TranslationHelper.GetExplanation(question, "es"));
        Assert.Equal("Originalno objašnjenje", TranslationHelper.GetExplanation(question, "sr"));

        question.ReplaceTranslations(Array.Empty<QuestionTranslation>());
        Assert.Equal("Originalno objašnjenje", TranslationHelper.GetExplanation(question, "de"));
    }

    [Fact]
    public void HintMethods_UseRequestedTranslationEnglishFallbackAndOriginalHints()
    {
        var question = CreateTranslatedQuestion();

        Assert.Equal("DE light", TranslationHelper.GetHintLight(question, "de"));
        Assert.Equal("DE medium", TranslationHelper.GetHintMedium(question, "de"));
        Assert.Equal("DE full", TranslationHelper.GetHintFull(question, "de"));

        Assert.Equal("EN light", TranslationHelper.GetHintLight(question, "es"));
        Assert.Equal("EN medium", TranslationHelper.GetHintMedium(question, "es"));
        Assert.Equal("EN full", TranslationHelper.GetHintFull(question, "es"));

        Assert.Equal("Original light", TranslationHelper.GetHintLight(question, "sr"));
        Assert.Equal("Original medium", TranslationHelper.GetHintMedium(question, "sr"));
        Assert.Equal("Original full", TranslationHelper.GetHintFull(question, "sr"));
    }

    [Fact]
    public void HintMethods_FallBackToOriginalWhenTranslationFieldIsMissing()
    {
        var question = new Question("Pitanje", 1, 1);
        question.SetHintFormula("Original light");
        question.SetHintClue("Original medium");
        question.SetHintFull("Original full");
        question.ReplaceTranslations(new[]
        {
            new QuestionTranslation(
                0,
                "en",
                "English question",
                hintLight: null,
                hintMedium: null,
                hintFull: null)
        });

        Assert.Equal("Original light", TranslationHelper.GetHintLight(question, "de"));
        Assert.Equal("Original medium", TranslationHelper.GetHintMedium(question, "de"));
        Assert.Equal("Original full", TranslationHelper.GetHintFull(question, "de"));
    }

    [Fact]
    public void GetOptionText_UsesRequestedTranslationThenEnglishFallbackThenOriginal()
    {
        var option = new QuestionOption("Originalna opcija", isCorrect: true);
        option.Translations.Add(new OptionTranslation(0, "en", "English option"));
        option.Translations.Add(new OptionTranslation(0, "de", "Deutsche Option"));

        Assert.Equal("Originalna opcija", TranslationHelper.GetOptionText(option, "sr"));
        Assert.Equal("Deutsche Option", TranslationHelper.GetOptionText(option, "de"));
        Assert.Equal("English option", TranslationHelper.GetOptionText(option, "es"));

        option.Translations.Clear();
        Assert.Equal("Originalna opcija", TranslationHelper.GetOptionText(option, "de"));
    }

    [Theory]
    [InlineData("de-DE", null, "de")]
    [InlineData("ES_mx", null, "sr")]
    [InlineData("unsupported", "en-US;q=0.8, de-DE;q=0.7", "en")]
    [InlineData(" ", "fr-FR, es-ES;q=0.5", "es")]
    public void ResolveLanguage_NormalizesSupportedSettingsAndHeaderValues(
        string? settings,
        string? header,
        string expected)
    {
        Assert.Equal(expected, TranslationHelper.ResolveLanguage(settings, header));
    }

    [Fact]
    public void ResolveSemanticsAltText_ExplicitValueWinsOverGeneratedDescription()
    {
        var result = TranslationHelper.ResolveSemanticsAltText(
            "Explicit accessible description",
            "$x^2$",
            ContentFormat.MarkdownWithMath);

        Assert.Equal("Explicit accessible description", result);
    }

    [Fact]
    public void ResolveSemanticsAltText_BlankValueGeneratesReadableMathDescription()
    {
        var result = TranslationHelper.ResolveSemanticsAltText(
            " ",
            "$x^2 × y$",
            ContentFormat.MarkdownWithMath);

        Assert.Equal("x squared times y", result);
    }

    [Fact]
    public void QuestionOptionAndStepSemantics_UseEntityOverrideOrTranslatedText()
    {
        var question = CreateTranslatedQuestion();
        question.SetSemanticsAltText("Question override");

        var option = new QuestionOption(
            "$x^2$",
            isCorrect: true,
            textFormat: ContentFormat.MarkdownWithMath,
            semanticsAltText: null);
        option.Translations.Add(new OptionTranslation(0, "en", "$y^2$"));

        var step = new QuestionStep(
            0,
            1,
            "$a × b$",
            hint: null,
            highlight: false,
            textFormat: ContentFormat.MarkdownWithMath,
            hintFormat: ContentFormat.PlainText,
            semanticsAltText: null);
        step.Translations.Add(new QuestionStepTranslation(0, "en", "$c ÷ d$"));

        Assert.Equal("Question override", TranslationHelper.GetQuestionSemanticsAltText(question, "de"));
        Assert.Equal("y squared", TranslationHelper.GetOptionSemanticsAltText(option, "en"));
        Assert.Equal("c divided by d", TranslationHelper.GetStepSemanticsAltText(step, "en"));
    }

    private static Question CreateTranslatedQuestion()
    {
        var question = new Question(
            "Originalno pitanje",
            difficulty: 1,
            categoryId: 1,
            explanation: "Originalno objašnjenje");
        question.SetHintFormula("Original light");
        question.SetHintClue("Original medium");
        question.SetHintFull("Original full");
        question.ReplaceTranslations(new[]
        {
            new QuestionTranslation(
                0,
                "en",
                "English question",
                explanation: "English explanation",
                hintLight: "EN light",
                hintMedium: "EN medium",
                hintFull: "EN full"),
            new QuestionTranslation(
                0,
                "de",
                "Deutsche Frage",
                explanation: "Deutsche Erklärung",
                hintLight: "DE light",
                hintMedium: "DE medium",
                hintFull: "DE full")
        });
        return question;
    }
}
