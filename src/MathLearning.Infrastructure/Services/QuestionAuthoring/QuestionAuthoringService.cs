using FluentValidation;
using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

public interface IQuestionAuthoringService
{
    Task<QuestionAuthoringApplyResult> CreateQuestionAsync(
        DbContext dbContext,
        QuestionAuthoringRequest request,
        string? actorUserId,
        CancellationToken cancellationToken);

    Task<QuestionAuthoringApplyResult> UpdateQuestionAsync(
        DbContext dbContext,
        Question question,
        QuestionAuthoringRequest request,
        string? actorUserId,
        CancellationToken cancellationToken);
}

public sealed record QuestionAuthoringApplyResult(
    Question Question,
    QuestionAuthoringRequest Request);

public sealed class QuestionAuthoringService : IQuestionAuthoringService
{
    private readonly IMathContentSanitizer sanitizer;
    private readonly IValidator<QuestionAuthoringRequest> validator;
    private readonly ILogger<QuestionAuthoringService> logger;

    public QuestionAuthoringService(
        IMathContentSanitizer sanitizer,
        IValidator<QuestionAuthoringRequest> validator,
        ILogger<QuestionAuthoringService> logger)
    {
        this.sanitizer = sanitizer;
        this.validator = validator;
        this.logger = logger;
    }

    public async Task<QuestionAuthoringApplyResult> CreateQuestionAsync(
        DbContext dbContext,
        QuestionAuthoringRequest request,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sanitizedRequest = await SanitizeAndValidateAsync(request, cancellationToken);
        var question = new Question(
            sanitizedRequest.Text,
            sanitizedRequest.Difficulty,
            sanitizedRequest.CategoryId,
            sanitizedRequest.Explanation);
        question.SetUpdatedBy(actorUserId ?? "system");

        ApplyQuestionFields(question, sanitizedRequest);
        SyncQuestionOptions(dbContext, question, sanitizedRequest);

        dbContext.Set<Question>().Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);

        ApplyAnswerMapping(question, sanitizedRequest);
        SyncQuestionSteps(dbContext, question, sanitizedRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created question {QuestionId} via shared authoring service.", question.Id);
        return new QuestionAuthoringApplyResult(question, sanitizedRequest);
    }

    public async Task<QuestionAuthoringApplyResult> UpdateQuestionAsync(
        DbContext dbContext,
        Question question,
        QuestionAuthoringRequest request,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(question);

        var sanitizedRequest = await SanitizeAndValidateAsync(request, cancellationToken);
        question.SetPreviousSnapshotJson(CreateQuestionSnapshot(question));
        if (!string.Equals(question.Text, sanitizedRequest.Text, StringComparison.Ordinal))
        {
            question.SetText(sanitizedRequest.Text);
        }

        ApplyQuestionFields(question, sanitizedRequest);
        SyncQuestionOptions(dbContext, question, sanitizedRequest);
        SyncQuestionSteps(dbContext, question, sanitizedRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        ApplyAnswerMapping(question, sanitizedRequest);
        question.SetUpdatedBy(actorUserId ?? "system");
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated question {QuestionId} via shared authoring service.", question.Id);
        return new QuestionAuthoringApplyResult(question, sanitizedRequest);
    }

    private async Task<QuestionAuthoringRequest> SanitizeAndValidateAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken)
    {
        var sanitizedRequest = QuestionAuthoringRequestSanitizer.Sanitize(request, sanitizer);
        var validationResult = await validator.ValidateAsync(sanitizedRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return sanitizedRequest;
    }

    private void ApplyQuestionFields(Question question, QuestionAuthoringRequest request)
    {
        question.SetType(request.Type);
        question.SetCategory(request.CategoryId);
        question.SetSubtopic(request.SubtopicId);
        question.SetDifficulty(request.Difficulty);
        question.SetExplanation(request.Explanation);
        question.SetHintFormula(ResolveHint(request.Hints, "formula", "light"));
        question.SetHintClue(ResolveHint(request.Hints, "clue", "medium"));
        question.SetHintFull(ResolveHint(request.Hints, "full"));
        question.SetTextFormat(request.TextFormat);
        question.SetExplanationFormat(request.ExplanationFormat);
        question.SetHintFormat(request.HintFormat);
        question.SetTextRenderMode(request.TextRenderMode);
        question.SetExplanationRenderMode(request.ExplanationRenderMode);
        question.SetHintRenderMode(request.HintRenderMode);
        question.SetSemanticsAltText(!string.IsNullOrWhiteSpace(request.SemanticsAltText)
            ? request.SemanticsAltText
            : sanitizer.GenerateSemanticsAltText(request.Text, request.TextFormat));
        question.SetHintDifficulty(request.Difficulty switch
        {
            <= 2 => 1,
            3 or 4 => 2,
            _ => 3
        });
    }

    private static void SyncQuestionOptions(DbContext dbContext, Question question, QuestionAuthoringRequest request)
    {
        var existingById = question.Options.ToDictionary(x => x.Id);
        var incomingIds = request.Options.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

        var toRemove = question.Options.Where(x => x.Id != 0 && !incomingIds.Contains(x.Id)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.Set<QuestionOption>().RemoveRange(toRemove);
            foreach (var option in toRemove)
            {
                question.Options.Remove(option);
            }
        }

        for (var i = 0; i < request.Options.Count; i++)
        {
            var incoming = request.Options[i];
            var order = i + 1;

            if (incoming.Id.HasValue && existingById.TryGetValue(incoming.Id.Value, out var existing))
            {
                existing.Update(
                    incoming.Text,
                    incoming.IsCorrect,
                    incoming.TextFormat,
                    incoming.RenderMode,
                    incoming.SemanticsAltText,
                    order);
                continue;
            }

            question.Options.Add(new QuestionOption(
                incoming.Text,
                incoming.IsCorrect,
                incoming.TextFormat,
                incoming.RenderMode,
                incoming.SemanticsAltText,
                order));
        }
    }

    private static void SyncQuestionSteps(DbContext dbContext, Question question, QuestionAuthoringRequest request)
    {
        var orderedSteps = request.Steps.OrderBy(x => x.Order).ToArray();
        var existingByOrder = question.Steps.ToDictionary(x => x.StepIndex);
        var incomingOrders = orderedSteps.Select(x => x.Order).ToHashSet();

        var toRemove = question.Steps.Where(x => !incomingOrders.Contains(x.StepIndex)).ToList();
        if (toRemove.Count > 0)
        {
            dbContext.Set<QuestionStep>().RemoveRange(toRemove);
            foreach (var step in toRemove)
            {
                question.Steps.Remove(step);
            }
        }

        foreach (var incoming in orderedSteps)
        {
            if (existingByOrder.TryGetValue(incoming.Order, out var existing))
            {
                existing.SetText(incoming.Text);
                existing.SetHint(incoming.Hint);
                existing.SetHighlight(incoming.Highlight);
                existing.SetStepIndex(incoming.Order);
                existing.SetTextFormat(incoming.TextFormat);
                existing.SetHintFormat(incoming.HintFormat);
                existing.SetTextRenderMode(incoming.TextRenderMode);
                existing.SetHintRenderMode(incoming.HintRenderMode);
                existing.SetSemanticsAltText(incoming.SemanticsAltText);
                continue;
            }

            var step = new QuestionStep(
                question.Id,
                incoming.Order,
                incoming.Text,
                incoming.Hint,
                incoming.Highlight,
                incoming.TextFormat,
                incoming.HintFormat,
                incoming.TextRenderMode,
                incoming.HintRenderMode,
                incoming.SemanticsAltText);

            question.Steps.Add(step);
            dbContext.Set<QuestionStep>().Add(step);
        }
    }

    private static void ApplyAnswerMapping(Question question, QuestionAuthoringRequest request)
    {
        if (!string.Equals(request.Type, "multiple_choice", StringComparison.OrdinalIgnoreCase))
        {
            question.SetCorrectOptionId(null);
            question.SetCorrectAnswer(request.CorrectAnswer);
            question.EnsureAnswerInvariant();
            return;
        }

        var orderedOptions = question.Options.OrderBy(x => x.Order).ToList();
        QuestionOption? resolved = null;

        if (request.CorrectOptionId.HasValue)
        {
            resolved = orderedOptions.FirstOrDefault(x => x.Id == request.CorrectOptionId.Value);
        }

        resolved ??= orderedOptions.FirstOrDefault(x => x.IsCorrect);
        resolved ??= !string.IsNullOrWhiteSpace(request.CorrectAnswer)
            ? orderedOptions.FirstOrDefault(x => string.Equals(x.Text, request.CorrectAnswer, StringComparison.Ordinal))
            : null;

        question.SetCorrectOptionId(resolved?.Id);
        question.SetCorrectAnswer(resolved?.Text ?? request.CorrectAnswer);
        question.EnsureAnswerInvariant();
    }

    private static string? ResolveHint(IReadOnlyList<QuestionHintDto> hints, params string[] keys)
        => hints.FirstOrDefault(x => keys.Any(key => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)))?.Text;

    private static string CreateQuestionSnapshot(Question question)
    {
        var snapshot = new
        {
            question.Id,
            question.Text,
            question.Type,
            question.CorrectOptionId,
            question.CorrectAnswer,
            question.Explanation,
            question.Difficulty,
            question.CategoryId,
            question.SubtopicId,
            question.UpdatedAt,
            question.UpdatedBy,
            Options = question.Options
                .OrderBy(x => x.Order)
                .Select(x => new
                {
                    x.Id,
                    x.Text,
                    x.IsCorrect,
                    x.Order
                })
                .ToArray(),
            Steps = question.Steps
                .OrderBy(x => x.StepIndex)
                .Select(x => new
                {
                    x.Id,
                    x.StepIndex,
                    x.Text,
                    x.Hint,
                    x.Highlight
                })
                .ToArray()
        };

        return JsonSerializer.Serialize(snapshot);
    }
}
