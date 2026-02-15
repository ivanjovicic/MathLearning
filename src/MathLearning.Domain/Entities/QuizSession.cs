using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MathLearning.Domain.Entities
{
    public class QuizSession
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
    }
}
