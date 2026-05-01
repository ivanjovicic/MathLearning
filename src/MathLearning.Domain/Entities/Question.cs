using System;
using System.Collections.Generic;
using System.Text;
using MathLearning.Domain.Enums;

namespace MathLearning.Domain.Entities
{
    public class Question
    {
        public int Id { get; private set; }
        public string Text { get; private set; } = "";
        public string Type { get; private set; } = "multiple_choice";
        public int? CorrectOptionId { get; private set; }
        public string? CorrectAnswer { get; private set; }
        public string? Explanation { get; private set; }
        public ContentFormat TextFormat { get; private set; } = ContentFormat.MarkdownWithMath;
        public ContentFormat ExplanationFormat { get; private set; } = ContentFormat.MarkdownWithMath;
        public ContentFormat HintFormat { get; private set; } = ContentFormat.MarkdownWithMath;
        public RenderMode TextRenderMode { get; private set; } = RenderMode.Auto;
        public RenderMode ExplanationRenderMode { get; private set; } = RenderMode.Auto;
        public RenderMode HintRenderMode { get; private set; } = RenderMode.Auto;
        public string? SemanticsAltText { get; private set; }
        public int Difficulty { get; private set; } = 1;
        public int CategoryId { get; private set; }
        public int SubtopicId { get; private set; }
        
        // 💡 Hints
        public string? HintFormula { get; private set; }
        public string? HintClue { get; private set; }
        public string? HintFull { get; private set; }
        public int HintDifficulty { get; private set; } = 1;
        
        public Category? Category { get; private set; }
        public Subtopic? Subtopic { get; private set; }

        public List<QuestionOption> Options { get; private set; } = new();
        public List<QuestionTranslation> Translations { get; private set; } = new();
        public List<QuestionStep> Steps { get; private set; } = new();

        public string PublishState { get; private set; } = QuestionPublishStates.Draft;
        public int CurrentVersionNumber { get; private set; }
        public Guid? CurrentDraftId { get; private set; }
        public string? PublishedByUserId { get; private set; }
        public DateTime? PublishedAtUtc { get; private set; }
        public string? UpdatedBy { get; private set; }
        public string? PreviousSnapshotJson { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAt { get; private set; }

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
            if (IsOpenAnswerType())
            {
                CorrectOptionId = null;
            }
            Touch();
        }

        public void SetCorrectAnswer(string? correctAnswer)
        {
            CorrectAnswer = correctAnswer;
            if (IsOpenAnswerType())
            {
                CorrectOptionId = null;
            }
            Touch();
        }

        public void SetCorrectOptionId(int? correctOptionId)
        {
            if (correctOptionId.HasValue && correctOptionId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(correctOptionId), "Correct option id must be positive.");
            }

            CorrectOptionId = correctOptionId;

            if (IsMultipleChoiceType() && correctOptionId.HasValue)
            {
                var optionText = Options.FirstOrDefault(x => x.Id == correctOptionId.Value)?.Text;
                if (!string.IsNullOrWhiteSpace(optionText))
                {
                    CorrectAnswer = optionText;
                }
            }

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

        public void SetTextFormat(ContentFormat format)
        {
            TextFormat = format;
            Touch();
        }

        public void SetExplanationFormat(ContentFormat format)
        {
            ExplanationFormat = format;
            Touch();
        }

        public void SetHintFormat(ContentFormat format)
        {
            HintFormat = format;
            Touch();
        }

        public void SetTextRenderMode(RenderMode renderMode)
        {
            TextRenderMode = renderMode;
            Touch();
        }

        public void SetExplanationRenderMode(RenderMode renderMode)
        {
            ExplanationRenderMode = renderMode;
            Touch();
        }

        public void SetHintRenderMode(RenderMode renderMode)
        {
            HintRenderMode = renderMode;
            Touch();
        }

        public void SetSemanticsAltText(string? semanticsAltText)
        {
            SemanticsAltText = semanticsAltText;
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
            SyncCorrectOptionFromOptions();
            Touch();
        }

        public void ReplaceSteps(IEnumerable<QuestionStep> steps)
        {
            Steps = steps.OrderBy(x => x.StepIndex).ToList();
            Touch();
        }

        public void ReplaceTranslations(IEnumerable<QuestionTranslation> translations)
        {
            Translations = translations.ToList();
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

        public void SetHintFull(string? hintFull)
        {
            HintFull = hintFull;
            Touch();
        }

        public void SetHintDifficulty(int hintDifficulty)
        {
            if (hintDifficulty < 1 || hintDifficulty > 3) 
                throw new ArgumentOutOfRangeException(nameof(hintDifficulty), "Hint difficulty must be 1..3");
            HintDifficulty = hintDifficulty;
            Touch();
        }

        public void SetPublishState(string publishState, string? actorUserId = null, DateTime? publishedAtUtc = null)
        {
            PublishState = string.IsNullOrWhiteSpace(publishState)
                ? QuestionPublishStates.Draft
                : publishState.Trim().ToLowerInvariant();
            PublishedByUserId = actorUserId;
            PublishedAtUtc = publishedAtUtc;
            Touch();
        }

        public void SetCurrentDraft(Guid? draftId)
        {
            CurrentDraftId = draftId;
            Touch();
        }

        public void SetCurrentVersionNumber(int versionNumber)
        {
            CurrentVersionNumber = Math.Max(0, versionNumber);
            Touch();
        }

        public void SetUpdatedBy(string? updatedBy)
        {
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim();
            Touch();
        }

        public void SetPreviousSnapshotJson(string? previousSnapshotJson)
        {
            PreviousSnapshotJson = previousSnapshotJson;
            Touch();
        }

        public void SoftDelete()
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            Touch();
        }

        public void Restore()
        {
            IsDeleted = false;
            DeletedAt = null;
            Touch();
        }

        public void SyncCorrectOptionFromOptions()
        {
            if (!IsMultipleChoiceType())
            {
                CorrectOptionId = null;
                return;
            }

            var correctOption = Options
                .OrderBy(x => x.Order)
                .FirstOrDefault(x => x.IsCorrect);

            if (correctOption is null)
            {
                CorrectOptionId = null;
                return;
            }

            CorrectOptionId = correctOption.Id > 0 ? correctOption.Id : null;
            CorrectAnswer = correctOption.Text;
        }

        public void EnsureAnswerInvariant()
        {
            if (IsMultipleChoiceType() && !CorrectOptionId.HasValue)
            {
                throw new InvalidOperationException("Multiple choice question requires CorrectOptionId.");
            }

            if (IsOpenAnswerType() && string.IsNullOrWhiteSpace(CorrectAnswer))
            {
                throw new InvalidOperationException("Open answer question requires CorrectAnswer.");
            }
        }

        private bool IsMultipleChoiceType()
            => string.Equals(Type, "multiple_choice", StringComparison.OrdinalIgnoreCase);

        private bool IsOpenAnswerType()
            => string.Equals(Type, "open_answer", StringComparison.OrdinalIgnoreCase);

        private void Touch() => UpdatedAt = DateTime.UtcNow;
    }
}
