using MathLearning.Application.DTOs.Quiz;
using MathLearning.Domain.Entities;

namespace MathLearning.Application.Services;

public interface ISrsService
{
    Task<QuestionStat> UpdateAsync(int userId, SrsUpdateDto dto);
}
