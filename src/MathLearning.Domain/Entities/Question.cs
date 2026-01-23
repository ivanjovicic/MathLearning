using System;
using System.Collections.Generic;
using System.Text;

namespace MathLearning.Domain.Entities
{
    public class Question
    {
        public int Id { get; private set; }
        public string Text { get; private set; } = "";
        public string Type { get; private set; } = "multiple_choice";
        public string? CorrectAnswer { get; private set; }
        public string? Explanation { get; private set; }
        public int Difficulty { get; private set; } = 1;
        public int CategoryId { get; private set; }
        public int SubtopicId { get; private set; }
        
        // 💡 Hints
        public string? HintFormula { get; private set; }
        public string? HintClue { get; private set; }
        public int HintDifficulty { get; private set; } = 1;
        
        public Category? Category { get; private set; }
        public Subtopic? Subtopic { get; private set; }

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

        public void SetType(string type)
        {
            Type = string.IsNullOrWhiteSpace(type) ? "multiple_choice" : type;
            Touch();
        }

        public void SetCorrectAnswer(string? correctAnswer)
        {
            CorrectAnswer = correctAnswer;
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

        public void SetSubtopic(int subtopicId)
        {
            SubtopicId = subtopicId;
            Touch();
        }

        public void ReplaceOptions(IEnumerable<QuestionOption> options)
        {
            Options = options.ToList();
            Touch();
        }

        // 💡 Hint Methods
        public void SetHintFormula(string? hintFormula)
        {
            HintFormula = hintFormula;
            Touch();
        }

        public void SetHintClue(string? hintClue)
        {
            HintClue = hintClue;
            Touch();
        }

        public void SetHintDifficulty(int hintDifficulty)
        {
            if (hintDifficulty < 1 || hintDifficulty > 3) 
                throw new ArgumentOutOfRangeException(nameof(hintDifficulty), "Hint difficulty must be 1..3");
            HintDifficulty = hintDifficulty;
            Touch();
        }

        private void Touch() => UpdatedAt = DateTime.UtcNow;
    }
}
