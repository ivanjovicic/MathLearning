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
        public int Order { get; private set; }
        public List<OptionTranslation> Translations { get; private set; } = new();

        private QuestionOption() { }

        public QuestionOption(string text, bool isCorrect, int order = 0)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
            Order = order;
        }

        public QuestionOption(
            string text,
            bool isCorrect,
            ContentFormat textFormat,
            RenderMode renderMode = RenderMode.Auto,
            string? semanticsAltText = null,
            int order = 0)
            : this(text, isCorrect, order)
        {
            TextFormat = textFormat;
            RenderMode = renderMode;
            SemanticsAltText = semanticsAltText;
        }

        public void Update(string text, bool isCorrect, int order = 0)
        {
            Text = string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("Option text is required") : text;
            IsCorrect = isCorrect;
            Order = order;
        }

        public void Update(
            string text,
            bool isCorrect,
            ContentFormat textFormat,
            RenderMode renderMode = RenderMode.Auto,
            string? semanticsAltText = null,
            int order = 0)
        {
            Update(text, isCorrect, order);
            TextFormat = textFormat;
            RenderMode = renderMode;
            SemanticsAltText = semanticsAltText;
        }
    }

}
