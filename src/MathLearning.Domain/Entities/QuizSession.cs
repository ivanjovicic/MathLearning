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
        public int UserId { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
