using System.Security.Claims;
using FluentValidation;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;

namespace MathLearning.Api.Endpoints;

public static class QuestionAuthoringEndpoints
{
    public static void MapQuestionAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questions")
            .RequireAuthorization()
            .WithTags("Question Authoring");

        group.MapPost("/validate", async (
            QuestionAuthoringRequest request,
            IValidator<QuestionAuthoringRequest> validator,
            IMathQuestionAuthoringService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await service.ValidateAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/preview", async (
            QuestionAuthoringRequest request,
            IValidator<QuestionAuthoringRequest> validator,
            IMathQuestionAuthoringService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await service.PreviewAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/save-draft", async (
            SaveQuestionDraftRequest request,
            IValidator<SaveQuestionDraftRequest> validator,
            IMathQuestionAuthoringService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await service.SaveDraftAsync(request, ResolveActorUserId(httpContext), cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/publish", async (
            PublishQuestionRequest request,
            IValidator<PublishQuestionRequest> validator,
            IMathQuestionAuthoringService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            var result = await service.PublishAsync(request, ResolveActorUserId(httpContext), cancellationToken);
            return result.Published ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{id:int}/versions", async (
            int id,
            IMathQuestionAuthoringService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetVersionsAsync(id, cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/{id:int}/validation", async (
            int id,
            IMathQuestionAuthoringService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetValidationAsync(id, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/{id:int}/revalidate", async (
            int id,
            IMathQuestionAuthoringService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RevalidateAsync(id, ResolveActorUserId(httpContext), cancellationToken);
            return Results.Ok(result);
        });
    }

    private static string? ResolveActorUserId(HttpContext httpContext)
        => httpContext.User.FindFirst("userId")?.Value
           ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? httpContext.User.Identity?.Name;
}
