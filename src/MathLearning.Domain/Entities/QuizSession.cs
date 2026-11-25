using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MathLearning.Domain.Entities
{
    public class QuizSession
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public string IssuedQuestionIdsJson { get; set; } = "[]";

        public void SetIssuedQuestionIds(IEnumerable<int> questionIds)
        {
            var normalizedIds = questionIds
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            IssuedQuestionIdsJson = JsonSerializer.Serialize(normalizedIds);
        }

        /// <summary>
        /// Freeze the exact issued question content (version, options, explanation, steps)
        /// so later publishes cannot change grading or feedback for this session.
        /// </summary>
        public void SetIssuedQuestions(IEnumerable<Question> questions)
        {
            ArgumentNullException.ThrowIfNull(questions);

            var snapshots = questions
                .Where(q => q.Id > 0)
                .GroupBy(q => q.Id)
                .Select(group => group.First())
                .OrderBy(q => q.Id)
                .Select(IssuedQuestionContentSnapshot.FromQuestion)
                .ToArray();

            var questionIds = snapshots.Select(x => x.QuestionId).ToArray();
            var manifest = new IssuedQuestionManifest(questionIds, snapshots);
            IssuedQuestionIdsJson = JsonSerializer.Serialize(manifest, JsonOptions);
        }

        public bool HasIssuedQuestion(int questionId)
        {
            if (questionId <= 0)
            {
                return false;
            }

            return GetIssuedQuestionIds().Contains(questionId);
        }

        public IReadOnlyList<int> GetIssuedQuestionIds()
        {
            if (string.IsNullOrWhiteSpace(IssuedQuestionIdsJson))
            {
                return Array.Empty<int>();
            }

            try
            {
                using var document = JsonDocument.Parse(IssuedQuestionIdsJson);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    if (document.RootElement.GetArrayLength() == 0)
                    {
                        return Array.Empty<int>();
                    }

                    var first = document.RootElement[0];
                    if (first.ValueKind == JsonValueKind.Number)
                    {
                        return JsonSerializer.Deserialize<int[]>(IssuedQuestionIdsJson) ?? Array.Empty<int>();
                    }

                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("questionId", out _))
                    {
                        return document.RootElement
                            .EnumerateArray()
                            .Select(item => item.TryGetProperty("questionId", out var idNode) && idNode.TryGetInt32(out var id)
                                ? id
                                : 0)
                            .Where(id => id > 0)
                            .Distinct()
                            .OrderBy(id => id)
                            .ToArray();
                    }
                }

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var manifest = JsonSerializer.Deserialize<IssuedQuestionManifest>(
                        IssuedQuestionIdsJson,
                        JsonOptions);
                    if (manifest?.QuestionIds is { Length: > 0 })
                    {
                        return manifest.QuestionIds
                            .Where(id => id > 0)
                            .Distinct()
                            .OrderBy(id => id)
                            .ToArray();
                    }

                    if (manifest?.Snapshots is { Length: > 0 })
                    {
                        return manifest.Snapshots
                            .Select(x => x.QuestionId)
                            .Where(id => id > 0)
                            .Distinct()
                            .OrderBy(id => id)
                            .ToArray();
                    }
                }
            }
            catch
            {
                return Array.Empty<int>();
            }

            return Array.Empty<int>();
        }

        public IssuedQuestionContentSnapshot? GetIssuedQuestionSnapshot(int questionId)
        {
            if (questionId <= 0 || string.IsNullOrWhiteSpace(IssuedQuestionIdsJson))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(IssuedQuestionIdsJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var manifest = JsonSerializer.Deserialize<IssuedQuestionManifest>(
                        IssuedQuestionIdsJson,
                        JsonOptions);
                    return manifest?.Snapshots?.FirstOrDefault(x => x.QuestionId == questionId);
                }

                if (document.RootElement.ValueKind == JsonValueKind.Array &&
                    document.RootElement.GetArrayLength() > 0 &&
                    document.RootElement[0].ValueKind == JsonValueKind.Object)
                {
                    var snapshots = JsonSerializer.Deserialize<IssuedQuestionContentSnapshot[]>(
                        IssuedQuestionIdsJson,
                        JsonOptions);
                    return snapshots?.FirstOrDefault(x => x.QuestionId == questionId);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }

    public sealed record IssuedQuestionManifest(
        int[] QuestionIds,
        IssuedQuestionContentSnapshot[] Snapshots);

    public sealed record IssuedQuestionContentSnapshot(
        int QuestionId,
        int VersionNumber,
        string Type,
        string Text,
        string? CorrectAnswer,
        int? CorrectOptionId,
        string? Explanation,
        IssuedQuestionOptionSnapshot[] Options,
        IssuedQuestionStepSnapshot[] Steps)
    {
        public static IssuedQuestionContentSnapshot FromQuestion(Question question)
        {
            ArgumentNullException.ThrowIfNull(question);

            var options = question.Options
                .OrderBy(o => o.Order)
                .Select(o => new IssuedQuestionOptionSnapshot(o.Id, o.Text, o.IsCorrect, o.Order))
                .ToArray();

            var steps = question.Steps
                .OrderBy(s => s.StepIndex)
                .Select(s => new IssuedQuestionStepSnapshot(s.StepIndex, s.Text, s.Hint, s.Highlight))
                .ToArray();

            return new IssuedQuestionContentSnapshot(
                question.Id,
                question.CurrentVersionNumber,
                question.Type,
                question.Text,
                question.CorrectAnswer,
                question.CorrectOptionId,
                question.Explanation,
                options,
                steps);
        }

        public void ApplyTo(Question question)
        {
            ArgumentNullException.ThrowIfNull(question);

            question.SetText(string.IsNullOrWhiteSpace(Text) ? question.Text : Text);
            question.SetType(Type);
            question.SetExplanation(Explanation);
            question.SetCorrectAnswer(CorrectAnswer);
            question.SetCurrentVersionNumber(VersionNumber);

            var options = Options
                .OrderBy(o => o.Order)
                .Select(o =>
                {
                    var option = new QuestionOption(o.Text, o.IsCorrect, o.Order);
                    if (o.Id > 0)
                    {
                        option.AssignPersistentId(o.Id);
                    }

                    return option;
                })
                .ToList();
            question.ReplaceOptions(options);

            if (CorrectOptionId is > 0)
            {
                question.SetCorrectOptionId(CorrectOptionId);
            }

            var steps = Steps
                .OrderBy(s => s.StepIndex)
                .Select(s => new QuestionStep(question.Id, s.StepIndex, s.Text, s.Hint, s.Highlight))
                .ToList();
            question.ReplaceSteps(steps);
        }
    }

    public sealed record IssuedQuestionOptionSnapshot(
        int Id,
        string Text,
        bool IsCorrect,
        int Order);

    public sealed record IssuedQuestionStepSnapshot(
        int StepIndex,
        string Text,
        string? Hint,
        bool Highlight);
}
