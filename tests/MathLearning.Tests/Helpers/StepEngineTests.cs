using MathLearning.Application.Helpers;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Enums;

namespace MathLearning.Tests.Helpers;

public sealed class StepEngineTests
{
    [Fact]
    public void GetSteps_StoredStepsTakePriorityAndAreReturnedInIndexOrder()
    {
        var question = NewQuestion("Koliko je 2 + 2?", explanation: "Fallback explanation");
        var second = new QuestionStep(
            questionId: 0,
            stepIndex: 2,
            text: "Drugi korak",
            hint: "Drugi hint",
            highlight: true,
            textFormat: ContentFormat.PlainText,
            hintFormat: ContentFormat.Markdown,
            textRenderMode: RenderMode.PlainText,
            hintRenderMode: RenderMode.Markdown,
            semanticsAltText: "Drugi korak opis");
        var first = new QuestionStep(0, 1, "Prvi korak", "Prvi hint", false);
        question.ReplaceSteps(new[] { second, first });

        var steps = StepEngine.GetSteps(question, "sr");

        Assert.Equal(2, steps.Count);
        Assert.Equal("Prvi korak", steps[0].Text);
        Assert.Equal("Drugi korak", steps[1].Text);
        Assert.True(steps[1].Highlight);
        Assert.Equal(ContentFormat.PlainText, steps[1].TextFormat);
        Assert.Equal(ContentFormat.Markdown, steps[1].HintFormat);
        Assert.Equal(RenderMode.PlainText, steps[1].TextRenderMode);
        Assert.Equal(RenderMode.Markdown, steps[1].HintRenderMode);
        Assert.Equal("Drugi korak opis", steps[1].SemanticsAltText);
    }

    [Fact]
    public void GetSteps_StoredStepUsesRequestedTranslationAndTranslatedHint()
    {
        var question = NewQuestion("Pitanje");
        var step = new QuestionStep(0, 1, "Srpski tekst", "Srpski hint", false);
        step.Translations.Add(new QuestionStepTranslation(0, "en", "English text", "English hint"));
        step.Translations.Add(new QuestionStepTranslation(0, "de", "Deutscher Text", "Deutscher Hinweis"));
        question.ReplaceSteps(new[] { step });

        var steps = StepEngine.GetSteps(question, "DE");

        var actual = Assert.Single(steps);
        Assert.Equal("Deutscher Text", actual.Text);
        Assert.Equal("Deutscher Hinweis", actual.Hint);
    }

    [Fact]
    public void GetSteps_MissingRequestedStepTranslationFallsBackToEnglishThenOriginalHint()
    {
        var question = NewQuestion("Pitanje");
        var step = new QuestionStep(0, 1, "Srpski tekst", "Srpski hint", false);
        step.Translations.Add(new QuestionStepTranslation(0, "en", "English text", hint: null));
        question.ReplaceSteps(new[] { step });

        var steps = StepEngine.GetSteps(question, "es");

        var actual = Assert.Single(steps);
        Assert.Equal("English text", actual.Text);
        Assert.Equal("Srpski hint", actual.Hint);
    }

    [Theory]
    [InlineData("Koliko je 27 + 18?", "sr", "Rezultat: 27 + 18 = 45", "Prenesi deseticu")]
    [InlineData("What is 27 + 18?", "en", "Result: 27 + 18 = 45", "Carry over")]
    public void GetSteps_AdditionGeneratesLocalizedCarrySteps(
        string text,
        string language,
        string expectedResult,
        string expectedCarryText)
    {
        var steps = StepEngine.GetSteps(NewQuestion(text), language);

        Assert.Contains(steps, step => step.Text.Contains(expectedCarryText, StringComparison.Ordinal));
        Assert.Equal(expectedResult, steps[^1].Text);
        Assert.True(steps[^1].Highlight);
    }

    [Theory]
    [InlineData("52 - 18", "sr", "Pozajmi deseticu", "Rezultat: 52 − 18 = 34")]
    [InlineData("52 − 18", "en", "Borrow", "Result: 52 − 18 = 34")]
    public void GetSteps_SubtractionGeneratesBorrowWhenNeeded(
        string text,
        string language,
        string expectedBorrow,
        string expectedResult)
    {
        var steps = StepEngine.GetSteps(NewQuestion(text), language);

        Assert.Contains(steps, step => step.Text.Contains(expectedBorrow, StringComparison.Ordinal));
        Assert.Equal(expectedResult, steps[^1].Text);
        Assert.True(steps[^1].Highlight);
    }

    [Theory]
    [InlineData("7 × 6", "sr", "7 + 7 + 7 + 7 + 7 + 7 = 42", "Rezultat: 7 × 6 = 42")]
    [InlineData("7 * 6", "en", "7 + 7 + 7 + 7 + 7 + 7 = 42", "Result: 7 × 6 = 42")]
    public void GetSteps_SmallMultiplicationUsesRepeatedAddition(
        string text,
        string language,
        string expectedExpansion,
        string expectedResult)
    {
        var steps = StepEngine.GetSteps(NewQuestion(text), language);

        Assert.Contains(steps, step => step.Text == expectedExpansion);
        Assert.Equal(expectedResult, steps[^1].Text);
    }

    [Fact]
    public void GetSteps_LargerMultiplicationUsesDistributiveBreakdown()
    {
        var steps = StepEngine.GetSteps(NewQuestion("12 × 23"), "en");

        Assert.Contains(steps, step => step.Text == "Break down 23 → 20 + 3");
        Assert.Contains(steps, step => step.Text == "12 × 20 = 240");
        Assert.Contains(steps, step => step.Text == "12 × 3 = 36");
        Assert.Contains(steps, step => step.Text == "Add: 240 + 36 = 276");
        Assert.Equal("Result: 12 × 23 = 276", steps[^1].Text);
    }

    [Theory]
    [InlineData("20 ÷ 5", "sr", 3, "Rezultat: 20 ÷ 5 = 4")]
    [InlineData("20 / 6", "en", 4, "Result: 20 ÷ 6 = 3 (remainder 2)")]
    public void GetSteps_DivisionHandlesExactAndRemainderResults(
        string text,
        string language,
        int expectedCount,
        string expectedResult)
    {
        var steps = StepEngine.GetSteps(NewQuestion(text), language);

        Assert.Equal(expectedCount, steps.Count);
        Assert.Equal(expectedResult, steps[^1].Text);
        Assert.True(steps[^1].Highlight);
    }

    [Theory]
    [InlineData("x + 7 = 19", "sr", "x = 12")]
    [InlineData("3x = 21", "en", "x = 7")]
    [InlineData("2x + 4 = 18", "en", "x = 7")]
    [InlineData("2x - 4 = 18", "sr", "x = 11")]
    [InlineData("2x − 4 = 18", "en", "x = 11")]
    public void GetSteps_LinearEquationsGenerateHighlightedSolution(
        string text,
        string language,
        string expectedSolution)
    {
        var steps = StepEngine.GetSteps(NewQuestion(text), language);

        var solution = Assert.Single(steps.Where(step => step.Highlight));
        Assert.Equal(expectedSolution, solution.Text);
    }

    [Fact]
    public void GetSteps_DivisionByZeroFallsBackToExplanationInsteadOfDividing()
    {
        var question = NewQuestion("10 / 0", "Deljenje nulom nije definisano.");

        var steps = StepEngine.GetSteps(question, "sr");

        var fallback = Assert.Single(steps);
        Assert.Equal("Deljenje nulom nije definisano.", fallback.Text);
        Assert.True(fallback.Highlight);
    }

    [Fact]
    public void GetSteps_UnsupportedQuestionUsesSingleExplanationFallback()
    {
        var question = NewQuestion("Objasni pojam funkcije.", "Funkcija preslikava elemente domena.");

        var steps = StepEngine.GetSteps(question, "sr");

        var fallback = Assert.Single(steps);
        Assert.Equal("Funkcija preslikava elemente domena.", fallback.Text);
        Assert.Null(fallback.Hint);
        Assert.True(fallback.Highlight);
    }

    [Fact]
    public void GetSteps_UnsupportedQuestionWithoutExplanationReturnsEmptyList()
    {
        var steps = StepEngine.GetSteps(NewQuestion("Objasni nepoznati koncept."), "sr");

        Assert.Empty(steps);
    }

    private static Question NewQuestion(string text, string? explanation = null) =>
        new(text, difficulty: 1, categoryId: 1, explanation);
}
