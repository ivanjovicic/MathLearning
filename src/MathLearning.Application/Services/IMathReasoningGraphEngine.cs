using MathLearning.Application.DTOs.Explanations;
using MathLearning.Domain.Explanations;

namespace MathLearning.Application.Services;

public interface IMathReasoningGraphEngine
{
    MathReasoningGraph Build(MathProblemDescriptor descriptor);
}
