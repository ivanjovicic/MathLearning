namespace MathLearning.Admin.Models;

public sealed class LatexInsertResult
{
    public bool Succeeded { get; init; }

    public string? FieldId { get; init; }

    public string? Value { get; init; }

    public int SelectionStart { get; init; }

    public int SelectionEnd { get; init; }

    public bool UsedFallback { get; init; }
}
