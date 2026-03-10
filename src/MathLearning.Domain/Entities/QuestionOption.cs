using System;
using System.Collections.Generic;
using System.Text;
using MathLearning.Domain.Enums;

namespace MathLearning.Domain.Entities
{
    public class QuestionOption
    {
        public int Id { get; private set; }
        public string Text { get; private set; } = "";
        public ContentFormat TextFormat { get; private set; } = ContentFormat.MarkdownWithMath;
        public RenderMode RenderMode { get; private set; } = RenderMode.Auto;
        public string? SemanticsAltText { get; private set; }
        public bool IsCorrect { get; private set; }
        public List<OptionTranslation> Translations { get; private set; } = new();

        private QuestionOption() { }

        public QuestionOption(string text, bool isCorrect)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
        }

        public QuestionOption(
            string text,
            bool isCorrect,
            ContentFormat textFormat,
            RenderMode renderMode = RenderMode.Auto,
            string? semanticsAltText = null)
            : this(text, isCorrect)
        {
            TextFormat = textFormat;
            RenderMode = renderMode;
            SemanticsAltText = semanticsAltText;
        }

        public void Update(string text, bool isCorrect)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
        }

        public void Update(
            string text,
            bool isCorrect,
            ContentFormat textFormat,
            RenderMode renderMode = RenderMode.Auto,
            string? semanticsAltText = null)
        {
            Update(text, isCorrect);
            TextFormat = textFormat;
            RenderMode = renderMode;
            SemanticsAltText = semanticsAltText;
        }
    }

}
