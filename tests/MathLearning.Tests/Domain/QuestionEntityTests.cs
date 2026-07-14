using MathLearning.Domain.Entities;

namespace MathLearning.Tests.Domain;

public sealed class QuestionEntityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingQuestionText(string? text)
    {
        Assert.Throws<ArgumentException>(() => new Question(text!, 1, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Constructor_RejectsDifficultyOutsideOneToFive(int difficulty)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Question("Question", difficulty, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_AcceptsSupportedDifficultyRange(int difficulty)
    {
        var question = new Question("Question", difficulty, 7, "Explanation");

        Assert.Equal("Question", question.Text);
        Assert.Equal(difficulty, question.Difficulty);
        Assert.Equal(7, question.CategoryId);
        Assert.Equal("Explanation", question.Explanation);
        Assert.Equal("multiple_choice", question.Type);
    }

    [Fact]
    public void SetType_MissingValueUsesMultipleChoiceDefault()
    {
        var question = NewQuestion();
        question.SetType("open_answer");

        question.SetType("   ");

        Assert.Equal("multiple_choice", question.Type);
    }

    [Fact]
    public void SetType_OpenAnswerClearsCorrectOptionButPreservesExplicitAnswer()
    {
        var question = NewQuestion();
        var option = OptionWithId(10, "Four", isCorrect: true, order: 1);
        question.ReplaceOptions(new[] { option });
        Assert.Equal(10, question.CorrectOptionId);

        question.SetCorrectAnswer("4");
        question.SetType("open_answer");

        Assert.Null(question.CorrectOptionId);
        Assert.Equal("4", question.CorrectAnswer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void SetCorrectOptionId_RejectsNonPositiveValues(int optionId)
    {
        var question = NewQuestion();

        Assert.Throws<ArgumentOutOfRangeException>(() => question.SetCorrectOptionId(optionId));
    }

    [Fact]
    public void ReplaceOptions_SelectsFirstCorrectOptionByOrderAndSynchronizesAnswer()
    {
        var laterCorrect = OptionWithId(20, "Later correct", isCorrect: true, order: 2);
        var firstCorrect = OptionWithId(10, "First correct", isCorrect: true, order: 1);
        var wrong = OptionWithId(5, "Wrong", isCorrect: false, order: 0);
        var question = NewQuestion();

        question.ReplaceOptions(new[] { laterCorrect, wrong, firstCorrect });

        Assert.Equal(10, question.CorrectOptionId);
        Assert.Equal("First correct", question.CorrectAnswer);
    }

    [Fact]
    public void ReplaceOptions_WithNoCorrectOptionClearsCorrectOptionId()
    {
        var question = NewQuestion();
        question.ReplaceOptions(new[] { OptionWithId(1, "Wrong", false, 1) });

        Assert.Null(question.CorrectOptionId);
        Assert.Throws<InvalidOperationException>(question.EnsureAnswerInvariant);
    }

    [Fact]
    public void EnsureAnswerInvariant_MultipleChoiceRequiresPersistedCorrectOption()
    {
        var question = NewQuestion();
        question.ReplaceOptions(new[] { OptionWithId(15, "Correct", true, 1) });

        question.EnsureAnswerInvariant();

        Assert.Equal(15, question.CorrectOptionId);
        Assert.Equal("Correct", question.CorrectAnswer);
    }

    [Fact]
    public void EnsureAnswerInvariant_OpenAnswerRequiresNonWhitespaceAnswer()
    {
        var question = NewQuestion();
        question.SetType("open_answer");
        question.SetCorrectAnswer("   ");

        var error = Assert.Throws<InvalidOperationException>(question.EnsureAnswerInvariant);
        Assert.Contains("CorrectAnswer", error.Message, StringComparison.Ordinal);

        question.SetCorrectAnswer("42");
        question.EnsureAnswerInvariant();
        Assert.Equal("42", question.CorrectAnswer);
    }

    [Fact]
    public void GetExpectedAnswerText_MultipleChoicePrefersCanonicalOptionOverLegacyCorrectAnswer()
    {
        var question = NewQuestion();
        question.ReplaceOptions(new[]
        {
            OptionWithId(10, "Canonical", true, 1),
            OptionWithId(11, "Wrong", false, 2)
        });
        question.SetCorrectAnswer("Legacy");

        var expected = question.GetExpectedAnswerText();

        Assert.Equal("Canonical", expected);
    }

    [Fact]
    public void MatchesSubmittedAnswer_MultipleChoiceIgnoresLegacyCorrectAnswerWhenCanonicalOptionExists()
    {
        var question = NewQuestion();
        question.ReplaceOptions(new[]
        {
            OptionWithId(10, "Canonical", true, 1),
            OptionWithId(11, "Wrong", false, 2)
        });
        question.SetCorrectAnswer("Legacy");

        Assert.True(question.MatchesSubmittedAnswer("Canonical"));
        Assert.True(question.MatchesSubmittedAnswer("10"));
        Assert.False(question.MatchesSubmittedAnswer("Legacy"));
    }

    [Fact]
    public void MatchesSubmittedAnswer_MultipleChoiceFallsBackToLegacyCorrectAnswerForLegacyRows()
    {
        var question = NewQuestion();
        question.ReplaceOptions(new[] { OptionWithId(10, "Wrong", false, 1) });
        question.SetCorrectAnswer("Legacy");

        Assert.True(question.MatchesSubmittedAnswer("Legacy"));
        Assert.False(question.MatchesSubmittedAnswer("Wrong"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void SetHintDifficulty_RejectsValuesOutsideOneToThree(int difficulty)
    {
        var question = NewQuestion();

        Assert.Throws<ArgumentOutOfRangeException>(() => question.SetHintDifficulty(difficulty));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SetHintDifficulty_AcceptsSupportedRange(int difficulty)
    {
        var question = NewQuestion();

        question.SetHintDifficulty(difficulty);

        Assert.Equal(difficulty, question.HintDifficulty);
    }

    [Fact]
    public void ReplaceSteps_OrdersStepsByIndex()
    {
        var question = NewQuestion();
        var third = new QuestionStep(0, 3, "Third");
        var first = new QuestionStep(0, 1, "First");
        var second = new QuestionStep(0, 2, "Second");

        question.ReplaceSteps(new[] { third, first, second });

        Assert.Equal(new[] { 1, 2, 3 }, question.Steps.Select(step => step.StepIndex).ToArray());
    }

    [Fact]
    public void SetPublishState_NormalizesStateAndTracksActorAndTimestamp()
    {
        var question = NewQuestion();
        var publishedAt = new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);

        question.SetPublishState("  PUBLISHED  ", "admin-user", publishedAt);

        Assert.Equal("published", question.PublishState);
        Assert.Equal("admin-user", question.PublishedByUserId);
        Assert.Equal(publishedAt, question.PublishedAtUtc);

        question.SetPublishState(" ");
        Assert.Equal(QuestionPublishStates.Draft, question.PublishState);
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(12, 12)]
    public void SetCurrentVersionNumber_ClampsNegativeValuesToZero(int input, int expected)
    {
        var question = NewQuestion();

        question.SetCurrentVersionNumber(input);

        Assert.Equal(expected, question.CurrentVersionNumber);
    }

    [Fact]
    public void SoftDeleteAndRestore_ToggleDeletionStateAndTimestamp()
    {
        var question = NewQuestion();
        var before = DateTime.UtcNow;

        question.SoftDelete();

        Assert.True(question.IsDeleted);
        Assert.NotNull(question.DeletedAt);
        Assert.InRange(question.DeletedAt!.Value, before, DateTime.UtcNow);

        question.Restore();

        Assert.False(question.IsDeleted);
        Assert.Null(question.DeletedAt);
    }

    [Theory]
    [InlineData("  editor-user  ", "editor-user")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void SetUpdatedBy_TrimsOrClearsValue(string? input, string? expected)
    {
        var question = NewQuestion();

        question.SetUpdatedBy(input);

        Assert.Equal(expected, question.UpdatedBy);
    }

    private static Question NewQuestion() => new("Question", 1, 1);

    private static QuestionOption OptionWithId(
        int id,
        string text,
        bool isCorrect,
        int order)
    {
        var option = new QuestionOption(text, isCorrect, order);
        var setter = typeof(QuestionOption)
            .GetProperty(nameof(QuestionOption.Id))!
            .GetSetMethod(nonPublic: true)!;
        setter.Invoke(option, new object[] { id });
        return option;
    }
}
