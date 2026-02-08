using System;
using System.Collections.Generic;
using System.Text;

namespace MathLearning.Domain.Entities
{
    public class QuestionOption
    {
        public int Id { get; private set; }
        public string Text { get; private set; } = "";
        public bool IsCorrect { get; private set; }
        public List<OptionTranslation> Translations { get; private set; } = new();

        private QuestionOption() { }

        public QuestionOption(string text, bool isCorrect)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
        }

        public void Update(string text, bool isCorrect)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
        }
    }

}
