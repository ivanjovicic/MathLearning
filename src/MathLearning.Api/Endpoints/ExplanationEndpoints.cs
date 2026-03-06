using FluentValidation;
using MathLearning.Application.DTOs.Explanations;
using MathLearning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Endpoints;

public static class ExplanationEndpoints
{
    public static void MapExplanationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/explanations")
            .RequireAuthorization()
            .WithTags("Explanations");

        group.MapGet("/problem/{problemId:int}", async (
            int problemId,
            [FromQuery] string? lang,
            IStepExplanationService service,
            CancellationToken ct) =>
        {
            try
            {
                var response = await service.GetForProblemAsync(problemId, string.IsNullOrWhiteSpace(lang) ? "en" : lang, ct);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Problem {problemId} not found." });
            }
        })
        .WithName("GetProblemExplanation")
        .WithSummary("Get structured explanation steps for a stored problem");

        group.MapPost("/generate", async (
            GenerateExplanationRequest request,
            IValidator<GenerateExplanationRequest> validator,
            IStepExplanationService service,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var response = await service.GenerateAsync(request, ct);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("GenerateExplanation")
        .WithSummary("Generate a structured explanation from free-form math input");

        group.MapPost("/mistake-analysis", async (
            MistakeAnalysisRequest request,
            IValidator<MistakeAnalysisRequest> validator,
            IStepExplanationService service,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var response = await service.AnalyzeMistakeAsync(request, ct);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("AnalyzeExplanationMistake")
        .WithSummary("Analyze a student's mistake and return targeted corrections");
    }
}
