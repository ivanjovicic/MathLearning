using MathLearning.Domain.Explanations;

namespace MathLearning.Application.Services;

public interface IFormulaReferenceService
{
    Task<IReadOnlyDictionary<string, FormulaReferenceDefinition>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);

    Task<FormulaReferenceDefinition?> GetByIdAsync(string id, CancellationToken ct = default);
}
