namespace MathLearning.Application.DTOs.Questions;

// Authored metadata. It is never sent to a learner before answer settlement.
public sealed record MathVisualPlanDto(
    int SchemaVersion,
    string TemplateId,
    int TemplateVersion,
    string Locale,
    string Operation,
    string TaskShape,
    int Rows,
    int Columns,
    IReadOnlyList<int> Groups);
