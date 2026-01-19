using System;
using System.Collections.Generic;
using System.Text;

namespace MathLearning.Domain.Entities
{
    public class Question
    {
        public int Id { get; private set; }
        public string Text { get; private set; } = "";
        public string? Explanation { get; private set; }
        public int Difficulty { get; private set; } = 1;
        public int CategoryId { get; private set; }
        public Category? Category { get; private set; }

        public List<QuestionOption> Options { get; private set; } = new();

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        private Question() { }

        public Question(string text, int difficulty, int categoryId, string? explanation = null)
        {
            SetText(text);
            SetDifficulty(difficulty);
            CategoryId = categoryId;
            Explanation = explanation;
        }

        public void SetText(string text)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Question text is required") : text;
            Touch();
        }

        public void SetDifficulty(int difficulty)
        {
            if (difficulty < 1 || difficulty > 5) throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty must be 1..5");
            Difficulty = difficulty;
            Touch();
        }

        public void SetExplanation(string? explanation)
        {
            Explanation = explanation;
            Touch();
        }

        public void SetCategory(int categoryId)
        {
            CategoryId = categoryId;
            Touch();
        }

        public void ReplaceOptions(IEnumerable<QuestionOption> options)
        {
            Options = options.ToList();
            Touch();
        }

        private void Touch() => UpdatedAt = DateTime.UtcNow;
    }
}
