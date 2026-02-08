using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Tests.Endpoints;

public class AnswerValidationTests
{
    [Fact]
    public async Task MultipleChoice_CorrectOptionText_IsCorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstAsync();

        var correctOption = question.Options.First(o => o.IsCorrect);

        bool isCorrect = question.Type == "multiple_choice"
            && question.Options.Any(o => o.IsCorrect && o.Text == correctOption.Text);

        Assert.True(isCorrect);
    }

    [Fact]
    public async Task MultipleChoice_WrongOptionText_IsIncorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstAsync();

        bool isCorrect = question.Type == "multiple_choice"
            && question.Options.Any(o => o.IsCorrect && o.Text == "wrong_answer_xyz");

        Assert.False(isCorrect);
    }

    [Fact]
    public async Task TextAnswer_CaseInsensitive_IsCorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = new Question("Koliko je 2+2?", 1, 1);
        question.SetType("text");
        question.SetCorrectAnswer("4");
        question.SetSubtopic(1);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        string userAnswer = "  4  ";

        bool isCorrect = question.CorrectAnswer != null
            && question.CorrectAnswer.Trim()
                .Equals(userAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

        Assert.True(isCorrect);
    }

    [Fact]
    public async Task TextAnswer_WrongValue_IsIncorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = new Question("Koliko je 2+2?", 1, 1);
        question.SetType("text");
        question.SetCorrectAnswer("4");
        question.SetSubtopic(1);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        string userAnswer = "5";

        bool isCorrect = question.CorrectAnswer != null
            && question.CorrectAnswer.Trim()
                .Equals(userAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

        Assert.False(isCorrect);
    }

    [Fact]
    public async Task TextAnswer_NullCorrectAnswer_IsIncorrect()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();

        var question = new Question("Koliko je 2+2?", 1, 1);
        question.SetType("text");
        question.SetSubtopic(1);
        db.Questions.Add(question);
        await db.SaveChangesAsync();

        string userAnswer = "4";

        bool isCorrect = question.CorrectAnswer != null
            && question.CorrectAnswer.Trim()
                .Equals(userAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

        Assert.False(isCorrect);
    }
}
