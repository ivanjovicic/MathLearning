using MathLearning.Api.Services;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Services;

public class FormulaReferenceServiceTests
{
    [Fact]
    public async Task GetByIdAsync_WhenDatabaseIsEmpty_ReturnsBuiltInFallback()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new FormulaReferenceService(db);

        var formula = await service.GetByIdAsync("quadratic_formula");

        Assert.NotNull(formula);
        Assert.Equal("quadratic_formula", formula!.Id);
        Assert.Contains("sqrt", formula.Latex, StringComparison.OrdinalIgnoreCase);
    }
}
