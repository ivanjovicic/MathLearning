using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MathLearning.Domain.Entities
{
    public class QuizSession
    {
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
                return JsonSerializer.Deserialize<int[]>(IssuedQuestionIdsJson) ?? Array.Empty<int>();
            }
            catch
            {
                return Array.Empty<int>();
            }
        }
    }
}
