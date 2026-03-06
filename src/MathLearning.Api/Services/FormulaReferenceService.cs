using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Domain.Explanations;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

public sealed class FormulaReferenceService : IFormulaReferenceService
{
    private static readonly IReadOnlyDictionary<string, FormulaReferenceDefinition> Defaults =
        new Dictionary<string, FormulaReferenceDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["quadratic_formula"] = new(
                "quadratic_formula",
                "Quadratic Formula",
                "x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi><mo>=</mo><mfrac><mrow><mo>-</mo><mi>b</mi><mo>&#x00B1;</mo><msqrt><msup><mi>b</mi><mn>2</mn></msup><mo>-</mo><mn>4</mn><mi>a</mi><mi>c</mi></msqrt></mrow><mrow><mn>2</mn><mi>a</mi></mrow></mfrac></mrow></math>",
                "Used to solve quadratic equations in the form ax^2 + bx + c = 0."),
            ["fraction_simplification_rule"] = new(
                "fraction_simplification_rule",
                "Fraction Simplification Rule",
                "\\frac{a}{b} = \\frac{a \\div d}{b \\div d}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mfrac><mi>a</mi><mi>b</mi></mfrac><mo>=</mo><mfrac><mrow><mi>a</mi><mo>&#x00F7;</mo><mi>d</mi></mrow><mrow><mi>b</mi><mo>&#x00F7;</mo><mi>d</mi></mrow></mfrac></mrow></math>",
                "Divide numerator and denominator by their greatest common divisor."),
            ["fraction_addition_rule"] = new(
                "fraction_addition_rule",
                "Fraction Addition Rule",
                "\\frac{a}{d} + \\frac{b}{d} = \\frac{a+b}{d}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mfrac><mi>a</mi><mi>d</mi></mfrac><mo>+</mo><mfrac><mi>b</mi><mi>d</mi></mfrac><mo>=</mo><mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mi>d</mi></mfrac></mrow></math>",
                "When denominators are equal, add only the numerators."),
            ["linear_equation_isolation"] = new(
                "linear_equation_isolation",
                "Linear Equation Isolation",
                "ax + b = c \\Rightarrow x = \\frac{c-b}{a}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>a</mi><mi>x</mi><mo>+</mo><mi>b</mi><mo>=</mo><mi>c</mi><mo>&#x21D2;</mo><mi>x</mi><mo>=</mo><mfrac><mrow><mi>c</mi><mo>-</mo><mi>b</mi></mrow><mi>a</mi></mfrac></mrow></math>",
                "Move the constant term first, then divide by the coefficient."),
            ["area_of_triangle"] = new(
                "area_of_triangle",
                "Area of Triangle",
                "A = \\frac{b \\cdot h}{2}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>A</mi><mo>=</mo><mfrac><mrow><mi>b</mi><mo>&#x22C5;</mo><mi>h</mi></mrow><mn>2</mn></mfrac></mrow></math>",
                "Triangle area equals base times height divided by two.")
        };

    private readonly ApiDbContext _db;

    public FormulaReferenceService(ApiDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, FormulaReferenceDefinition>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var requested = ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 0)
            return new Dictionary<string, FormulaReferenceDefinition>(StringComparer.OrdinalIgnoreCase);

        var dbEntries = await _db.MathFormulaReferences
            .AsNoTracking()
            .Where(x => requested.Contains(x.Id))
            .ToListAsync(ct);

        var result = new Dictionary<string, FormulaReferenceDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in dbEntries)
            result[entry.Id] = Map(entry);

        foreach (var id in requested)
        {
            if (!result.ContainsKey(id) && Defaults.TryGetValue(id, out var fallback))
                result[id] = fallback;
        }

        return result;
    }

    public async Task<FormulaReferenceDefinition?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var entry = await _db.MathFormulaReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entry is not null)
            return Map(entry);

        return Defaults.TryGetValue(id, out var fallback) ? fallback : null;
    }

    private static FormulaReferenceDefinition Map(MathFormulaReferenceEntity entry) =>
        new(entry.Id, entry.Name, entry.Latex, entry.MathMl, entry.Description);
}
