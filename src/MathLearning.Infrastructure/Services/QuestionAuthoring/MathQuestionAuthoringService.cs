using System.Text.Json;
using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathLearning.Infrastructure.Services.QuestionAuthoring;

public sealed partial class MathQuestionAuthoringService :
    IMathQuestionAuthoringService,
    IMathQuestionValidationService,
    IQuestionVersioningService
{
    private static readonly TimeSpan ValidationCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PreviewCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ApiDbContext db;
    private readonly ILogger<MathQuestionAuthoringService> logger;
    private readonly HybridCacheService cache;
    private readonly IMathContentLinter linter;
    private readonly ILatexValidationService latexValidation;
    private readonly IMathNormalizationService normalization;
    private readonly IMathEquivalenceService equivalence;
    private readonly IStepExplanationValidationService stepValidation;
    private readonly IDifficultyEstimationService difficulty;
    private readonly IQuestionPreviewService previewService;
    private readonly IQuestionPublishGuardService publishGuard;
    private readonly IQuestionAutoHintGenerator autoHintGenerator;
    private readonly IQuestionAuthoringService questionAuthoringService;
    private readonly IMathContentSanitizer sanitizer;

    public MathQuestionAuthoringService(
        ApiDbContext db,
        ILogger<MathQuestionAuthoringService> logger,
        HybridCacheService cache,
        IMathContentLinter linter,
        ILatexValidationService latexValidation,
        IMathNormalizationService normalization,
        IMathEquivalenceService equivalence,
        IStepExplanationValidationService stepValidation,
        IDifficultyEstimationService difficulty,
        IQuestionPreviewService previewService,
        IQuestionPublishGuardService publishGuard,
        IQuestionAutoHintGenerator autoHintGenerator,
        IQuestionAuthoringService questionAuthoringService,
        IMathContentSanitizer sanitizer)
    {
        this.db = db;
        this.logger = logger;
        this.cache = cache;
        this.linter = linter;
        this.latexValidation = latexValidation;
        this.normalization = normalization;
        this.equivalence = equivalence;
        this.stepValidation = stepValidation;
        this.difficulty = difficulty;
        this.previewService = previewService;
        this.publishGuard = publishGuard;
        this.autoHintGenerator = autoHintGenerator;
        this.questionAuthoringService = questionAuthoringService;
        this.sanitizer = sanitizer;
    }

    public async Task<ValidateQuestionResponse> ValidateAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken)
    {
        var key = $"question-authoring:validate:{QuestionAuthoringContentSupport.ComputeContentHash(request)}";
        return await cache.GetOrCreateAsync(
            key,
            async ct => (await RunPipelineAsync(request, ct)).Validation,
            ValidationCacheTtl,
            ValidationCacheTtl,
            cancellationToken);
    }

    public async Task<PreviewQuestionResponse> PreviewAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken)
    {
        var key = $"question-authoring:preview:{QuestionAuthoringContentSupport.ComputeContentHash(request)}";
        return await cache.GetOrCreateAsync(
            key,
            async ct => (await RunPipelineAsync(request, ct)).Preview,
            PreviewCacheTtl,
            PreviewCacheTtl,
            cancellationToken);
    }

    public async Task<SaveQuestionDraftResponse> SaveDraftAsync(
        SaveQuestionDraftRequest request,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.Content.QuestionId is { } questionId &&
            !await db.Questions.AnyAsync(x => x.Id == questionId, cancellationToken))
        {
            throw new InvalidOperationException($"Question {questionId} was not found.");
        }

        var pipeline = await RunPipelineAsync(request.Content, cancellationToken);
        var latestDraft = await db.QuestionDrafts
            .Where(x => x.QuestionId == request.Content.QuestionId)
            .OrderByDescending(x => x.DraftVersion)
            .FirstOrDefaultAsync(cancellationToken);

        var draft = new QuestionDraft
        {
            Id = Guid.NewGuid(),
            QuestionId = request.Content.QuestionId,
            PreviousDraftId = latestDraft?.Id,
            DraftVersion = (latestDraft?.DraftVersion ?? 0) + 1,
            ContentJson = JsonSerializer.Serialize(pipeline.EffectiveRequest, QuestionAuthoringContentSupport.JsonOptions),
            NormalizedContentJson = JsonSerializer.Serialize(pipeline.Preview.Preview.Normalized, QuestionAuthoringContentSupport.JsonOptions),
            ContentHash = pipeline.ContentHash,
            PublishState = pipeline.Validation.Summary.CanPublish ? QuestionPublishStates.Validated : QuestionPublishStates.Draft,
            ValidationStatus = pipeline.Validation.Summary.Status,
            ChangeReason = request.ChangeReason ?? request.Content.ChangeReason,
            AuthorUserId = actorUserId,
            EditorUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.QuestionDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        var validationEntity = CreateValidationResultEntity(draft.Id, pipeline);
        draft.LatestValidationResultId = validationEntity.Id;
        db.QuestionValidationResults.Add(validationEntity);
        db.QuestionValidationIssues.AddRange(validationEntity.Issues);

        await UpsertPreviewCacheAsync(draft.Id, pipeline.ContentHash, pipeline.Preview, cancellationToken);
        db.QuestionAuthoringAuditLogs.Add(new QuestionAuthoringAuditLog
        {
            DraftId = draft.Id,
            QuestionId = draft.QuestionId,
            Action = "save_draft",
            ActorUserId = actorUserId,
            AfterJson = draft.ContentJson,
            Reason = draft.ChangeReason,
            OccurredAtUtc = DateTime.UtcNow
        });

        if (draft.QuestionId.HasValue)
        {
            var question = await db.Questions.FirstAsync(x => x.Id == draft.QuestionId.Value, cancellationToken);
            question.SetCurrentDraft(draft.Id);
            if (!string.Equals(question.PublishState, QuestionPublishStates.Published, StringComparison.Ordinal))
            {
                question.SetPublishState(draft.PublishState);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Saved question draft {DraftId} for question {QuestionId}. Status={Status} Errors={ErrorCount} Warnings={WarningCount}",
            draft.Id,
            draft.QuestionId,
            pipeline.Validation.Summary.Status,
            pipeline.Validation.Summary.ErrorCount,
            pipeline.Validation.Summary.WarningCount);

        return new SaveQuestionDraftResponse(
            draft.Id,
            draft.DraftVersion,
            draft.PublishState,
            pipeline.Validation);
    }

    public async Task<PublishQuestionResponse> PublishAsync(
        PublishQuestionRequest request,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var draft = await db.QuestionDrafts
            .Include(x => x.LatestValidationResult)
            .ThenInclude(x => x!.Issues)
            .FirstOrDefaultAsync(x => x.Id == request.DraftId, cancellationToken)
            ?? throw new InvalidOperationException($"Draft {request.DraftId} was not found.");

        var authoringRequest = DeserializeDraft(draft);
        var pipeline = await RunPipelineAsync(authoringRequest, cancellationToken);
        if (!pipeline.Validation.Summary.CanPublish)
        {
            logger.LogWarning(
                "Publish blocked for draft {DraftId}. Errors={ErrorCount} Warnings={WarningCount}",
                draft.Id,
                pipeline.Validation.Summary.ErrorCount,
                pipeline.Validation.Summary.WarningCount);

            return new PublishQuestionResponse(
                false,
                draft.QuestionId ?? 0,
                0,
                0,
                draft.PublishState,
                pipeline.Validation.Summary);
        }

        var useTransaction = db.Database.IsRelational();
        await using var transaction = useTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var question = await LoadQuestionForPublishAsync(draft.QuestionId, cancellationToken);
            if (question is null)
            {
                var createResult = await questionAuthoringService.CreateQuestionAsync(
                    db,
                    pipeline.EffectiveRequest,
                    actorUserId,
                    cancellationToken);
                question = createResult.Question;
            }
            else
            {
                await questionAuthoringService.UpdateQuestionAsync(
                    db,
                    question,
                    pipeline.EffectiveRequest,
                    actorUserId,
                    cancellationToken);
            }

            var previousVersion = await db.QuestionVersions
                .Where(x => x.QuestionId == question.Id)
                .OrderByDescending(x => x.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var version = new QuestionVersion
            {
                QuestionId = question.Id,
                SourceDraftId = draft.Id,
                PreviousVersionId = previousVersion?.Id,
                VersionNumber = (previousVersion?.VersionNumber ?? 0) + 1,
                SnapshotJson = JsonSerializer.Serialize(pipeline.EffectiveRequest, QuestionAuthoringContentSupport.JsonOptions),
                NormalizedSnapshotJson = JsonSerializer.Serialize(pipeline.Preview.Preview.Normalized, QuestionAuthoringContentSupport.JsonOptions),
                PublishState = QuestionPublishStates.Published,
                ChangeReason = request.ChangeReason ?? draft.ChangeReason,
                AuthorUserId = draft.AuthorUserId ?? actorUserId,
                EditorUserId = actorUserId,
                CreatedAtUtc = DateTime.UtcNow,
                PublishedAtUtc = DateTime.UtcNow
            };

            question.SetCurrentDraft(draft.Id);
            question.SetCurrentVersionNumber(version.VersionNumber);
            question.SetPublishState(QuestionPublishStates.Published, actorUserId, DateTime.UtcNow);
            draft.QuestionId = question.Id;
            draft.PublishState = QuestionPublishStates.Published;
            draft.ValidationStatus = pipeline.Validation.Summary.Status;
            draft.EditorUserId = actorUserId;
            draft.UpdatedAtUtc = DateTime.UtcNow;

            await ReplaceValidationForDraftAsync(draft, pipeline, actorUserId, cancellationToken);

            db.QuestionVersions.Add(version);
            db.QuestionAuthoringAuditLogs.Add(new QuestionAuthoringAuditLog
            {
                DraftId = draft.Id,
                QuestionId = question.Id,
                Action = "publish_question",
                ActorUserId = actorUserId,
                AfterJson = version.SnapshotJson,
                Reason = request.ChangeReason ?? draft.ChangeReason,
                OccurredAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            logger.LogInformation(
                "Published question {QuestionId} from draft {DraftId}. Version={VersionNumber}",
                question.Id,
                draft.Id,
                version.VersionNumber);

            return new PublishQuestionResponse(
                true,
                question.Id,
                version.Id,
                version.VersionNumber,
                QuestionPublishStates.Published,
                pipeline.Validation.Summary);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<QuestionVersionHistoryItemDto>> GetVersionsAsync(int questionId, CancellationToken cancellationToken)
    {
        return await db.QuestionVersions
            .AsNoTracking()
            .Where(x => x.QuestionId == questionId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new QuestionVersionHistoryItemDto(
                x.Id,
                x.VersionNumber,
                x.PublishState,
                x.AuthorUserId,
                x.EditorUserId,
                x.ChangeReason,
                x.CreatedAtUtc,
                x.PublishedAtUtc,
                x.PreviousVersionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionValidationHistoryDto?> GetValidationAsync(int questionId, CancellationToken cancellationToken)
    {
        var draftId = await db.Questions
            .Where(x => x.Id == questionId)
            .Select(x => x.CurrentDraftId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!draftId.HasValue)
        {
            draftId = await db.QuestionDrafts
                .Where(x => x.QuestionId == questionId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!draftId.HasValue)
        {
            return null;
        }

        var validation = await db.QuestionValidationResults
            .AsNoTracking()
            .Include(x => x.Issues)
            .Where(x => x.DraftId == draftId.Value)
            .OrderByDescending(x => x.ValidatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return validation is null ? null : ToValidationHistoryDto(validation);
    }

    public async Task<QuestionValidationHistoryDto> RevalidateAsync(int questionId, string? actorUserId, CancellationToken cancellationToken)
    {
        var question = await db.Questions
            .Include(x => x.Options)
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken)
            ?? throw new InvalidOperationException($"Question {questionId} was not found.");

        var draft = await db.QuestionDrafts
            .FirstOrDefaultAsync(x => x.Id == question.CurrentDraftId, cancellationToken);

        if (draft is null)
        {
            draft = await CreateDraftFromQuestionAsync(question, actorUserId, cancellationToken);
        }

        var authoringRequest = DeserializeDraft(draft);
        var pipeline = await RunPipelineAsync(authoringRequest, cancellationToken);
        await ReplaceValidationForDraftAsync(draft, pipeline, actorUserId, cancellationToken);
        draft.ValidationStatus = pipeline.Validation.Summary.Status;
        draft.PublishState = pipeline.Validation.Summary.CanPublish ? QuestionPublishStates.Validated : QuestionPublishStates.Draft;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        db.QuestionAuthoringAuditLogs.Add(new QuestionAuthoringAuditLog
        {
            DraftId = draft.Id,
            QuestionId = questionId,
            Action = "revalidate_question",
            ActorUserId = actorUserId,
            AfterJson = draft.ContentJson,
            Reason = draft.ChangeReason,
            OccurredAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToValidationHistoryDto(await db.QuestionValidationResults
            .AsNoTracking()
            .Include(x => x.Issues)
            .FirstAsync(x => x.Id == draft.LatestValidationResultId, cancellationToken));
    }

    private async Task<PipelineExecution> RunPipelineAsync(QuestionAuthoringRequest request, CancellationToken cancellationToken)
    {
        var effectiveHints = request.Hints.Count > 0
            ? request.Hints
            : await autoHintGenerator.GenerateAsync(request, cancellationToken);
        var effectiveRequest = request with { Hints = effectiveHints };
        var sanitizedRequest = SanitizeRequest(effectiveRequest);

        var lint = linter.Lint(effectiveRequest);
        var latex = latexValidation.Validate(sanitizedRequest);
        var normalized = normalization.Normalize(sanitizedRequest);
        var equivalenceChecks = await BuildEquivalenceChecksAsync(sanitizedRequest, cancellationToken);
        var stepResults = await stepValidation.ValidateAsync(sanitizedRequest, cancellationToken);
        var difficultyResult = difficulty.Estimate(sanitizedRequest, normalized);
        var summary = publishGuard.BuildSummary(lint, latex, equivalenceChecks, stepResults);
        var preview = previewService.BuildPreview(
            sanitizedRequest,
            lint,
            latex,
            normalized,
            equivalenceChecks,
            stepResults,
            difficultyResult,
            summary);
        var validation = new ValidateQuestionResponse(
            summary,
            lint,
            latex,
            normalized,
            equivalenceChecks,
            stepResults,
            difficultyResult);

        logger.LogDebug(
            "Question authoring validation completed. QuestionId={QuestionId} Status={Status} Errors={ErrorCount} Warnings={WarningCount}",
            effectiveRequest.QuestionId,
            summary.Status,
            summary.ErrorCount,
            summary.WarningCount);

        return new PipelineExecution(
            sanitizedRequest,
            QuestionAuthoringContentSupport.ComputeContentHash(sanitizedRequest),
            validation,
            new PreviewQuestionResponse(preview));
    }

    private async Task<IReadOnlyList<EquivalentAnswerResultDto>> BuildEquivalenceChecksAsync(
        QuestionAuthoringRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CorrectAnswer))
        {
            return Array.Empty<EquivalentAnswerResultDto>();
        }

        var correctOptions = request.Options.Where(x => x.IsCorrect).ToArray();
        if (correctOptions.Length == 0)
        {
            var validation = await equivalence.ValidateExpressionAsync(request.CorrectAnswer!, cancellationToken);
            return validation.IsValid
                ? Array.Empty<EquivalentAnswerResultDto>()
                : [new EquivalentAnswerResultDto(false, request.CorrectAnswer!, request.CorrectAnswer!, "validation", validation.ErrorMessage)];
        }

        var results = new List<EquivalentAnswerResultDto>(correctOptions.Length);
        foreach (var option in correctOptions)
        {
            results.Add(await equivalence.AreEquivalentAsync(
                request.CorrectAnswer!,
                option.Text,
                new MathEquivalenceContext(),
                cancellationToken));
        }

        return results;
    }

    private QuestionValidationResult CreateValidationResultEntity(Guid draftId, PipelineExecution pipeline)
    {
        var summary = pipeline.Validation.Summary;
        var entity = new QuestionValidationResult
        {
            Id = Guid.NewGuid(),
            DraftId = draftId,
            ContentHash = pipeline.ContentHash,
            Status = summary.Status,
            HasErrors = summary.ErrorCount > 0,
            HasWarnings = summary.WarningCount > 0,
            IssueCount = summary.Issues.Count,
            SummaryJson = JsonSerializer.Serialize(summary, QuestionAuthoringContentSupport.JsonOptions),
            PreviewPayloadJson = JsonSerializer.Serialize(pipeline.Preview.Preview, QuestionAuthoringContentSupport.JsonOptions),
            ValidatedAtUtc = DateTime.UtcNow
        };

        entity.Issues = summary.Issues
            .Select(issue => new QuestionValidationIssue
            {
                ValidationResultId = entity.Id,
                Stage = issue.Stage,
                RuleId = issue.RuleId,
                Severity = issue.Severity,
                Message = issue.Message,
                FieldPath = issue.FieldPath,
                Suggestion = issue.Suggestion
            })
            .ToList();

        return entity;
    }

    private async Task ReplaceValidationForDraftAsync(
        QuestionDraft draft,
        PipelineExecution pipeline,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var validation = CreateValidationResultEntity(draft.Id, pipeline);
        draft.LatestValidationResultId = validation.Id;
        draft.NormalizedContentJson = JsonSerializer.Serialize(pipeline.Preview.Preview.Normalized, QuestionAuthoringContentSupport.JsonOptions);
        draft.ContentHash = pipeline.ContentHash;
        draft.ValidationStatus = pipeline.Validation.Summary.Status;
        draft.EditorUserId = actorUserId;
        draft.UpdatedAtUtc = DateTime.UtcNow;

        db.QuestionValidationResults.Add(validation);
        db.QuestionValidationIssues.AddRange(validation.Issues);
        await UpsertPreviewCacheAsync(draft.Id, pipeline.ContentHash, pipeline.Preview, cancellationToken);
    }

    private async Task UpsertPreviewCacheAsync(
        Guid draftId,
        string contentHash,
        PreviewQuestionResponse preview,
        CancellationToken cancellationToken)
    {
        var existing = await db.QuestionPreviewCaches
            .Where(x => x.DraftId == draftId || x.ContentHash == contentHash)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            db.QuestionPreviewCaches.RemoveRange(existing);
        }

        db.QuestionPreviewCaches.Add(new QuestionPreviewCache
        {
            Id = Guid.NewGuid(),
            DraftId = draftId,
            ContentHash = contentHash,
            PreviewPayloadJson = JsonSerializer.Serialize(preview.Preview, QuestionAuthoringContentSupport.JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });
    }

    private async Task<Question?> LoadQuestionForPublishAsync(int? questionId, CancellationToken cancellationToken)
    {
        if (!questionId.HasValue)
        {
            return null;
        }

        return await db.Questions
            .Include(x => x.Options)
            .ThenInclude(x => x.Translations)
            .Include(x => x.Steps)
            .ThenInclude(x => x.Translations)
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == questionId.Value, cancellationToken);
    }

    private async Task<QuestionDraft> CreateDraftFromQuestionAsync(Question question, string? actorUserId, CancellationToken cancellationToken)
    {
        var resolvedCorrectOptionId = question.CorrectOptionId
            ?? question.Options
                .OrderBy(x => x.Order)
                .FirstOrDefault(x => x.IsCorrect)?.Id;

        var request = new QuestionAuthoringRequest(
            question.Id,
            question.Text,
            question.Type,
            question.CorrectAnswer,
            question.Explanation,
            question.Difficulty,
            question.CategoryId,
            question.SubtopicId,
            question.Options
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id)
                .Select(x => new QuestionAuthoringOptionDto(
                    x.Id,
                    x.Text,
                    resolvedCorrectOptionId.HasValue ? x.Id == resolvedCorrectOptionId.Value : x.IsCorrect,
                    x.TextFormat,
                    x.RenderMode,
                    x.SemanticsAltText))
                .ToArray(),
            BuildHints(question),
            question.Steps
                .OrderBy(x => x.StepIndex)
                .Select(x => new StepExplanationAuthoringDto(
                    x.StepIndex,
                    x.Text,
                    x.Hint,
                    x.Highlight,
                    x.TextFormat,
                    x.HintFormat,
                    x.TextRenderMode,
                    x.HintRenderMode,
                    x.SemanticsAltText))
                .ToArray(),
            "system_revalidate",
            question.Steps.Count > 0,
            question.TextFormat,
            question.ExplanationFormat,
            question.HintFormat,
            question.TextRenderMode,
            question.ExplanationRenderMode,
            question.HintRenderMode,
            question.SemanticsAltText,
            resolvedCorrectOptionId);

        var pipeline = await RunPipelineAsync(request, cancellationToken);
        var latestDraft = await db.QuestionDrafts
            .Where(x => x.QuestionId == question.Id)
            .OrderByDescending(x => x.DraftVersion)
            .FirstOrDefaultAsync(cancellationToken);

        var draft = new QuestionDraft
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            PreviousDraftId = latestDraft?.Id,
            DraftVersion = (latestDraft?.DraftVersion ?? 0) + 1,
            ContentJson = JsonSerializer.Serialize(pipeline.EffectiveRequest, QuestionAuthoringContentSupport.JsonOptions),
            NormalizedContentJson = JsonSerializer.Serialize(pipeline.Preview.Preview.Normalized, QuestionAuthoringContentSupport.JsonOptions),
            ContentHash = pipeline.ContentHash,
            PublishState = question.PublishState,
            ValidationStatus = pipeline.Validation.Summary.Status,
            ChangeReason = "system_revalidate",
            AuthorUserId = actorUserId,
            EditorUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.QuestionDrafts.Add(draft);
        question.SetCurrentDraft(draft.Id);
        await db.SaveChangesAsync(cancellationToken);

        var validation = CreateValidationResultEntity(draft.Id, pipeline);
        draft.LatestValidationResultId = validation.Id;
        db.QuestionValidationResults.Add(validation);
        db.QuestionValidationIssues.AddRange(validation.Issues);
        await UpsertPreviewCacheAsync(draft.Id, pipeline.ContentHash, pipeline.Preview, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return draft;
    }

    private static QuestionHintDto[] BuildHints(Question question)
    {
        var hints = new List<QuestionHintDto>();
        if (!string.IsNullOrWhiteSpace(question.HintFormula))
        {
            hints.Add(new QuestionHintDto("formula", question.HintFormula));
        }

        if (!string.IsNullOrWhiteSpace(question.HintClue))
        {
            hints.Add(new QuestionHintDto("clue", question.HintClue));
        }

        if (!string.IsNullOrWhiteSpace(question.HintFull))
        {
            hints.Add(new QuestionHintDto("full", question.HintFull));
        }

        return hints.ToArray();
    }

    private QuestionAuthoringRequest SanitizeRequest(QuestionAuthoringRequest request)
        => QuestionAuthoringRequestSanitizer.Sanitize(request, sanitizer);

    private static QuestionAuthoringRequest DeserializeDraft(QuestionDraft draft)
        => JsonSerializer.Deserialize<QuestionAuthoringRequest>(draft.ContentJson, QuestionAuthoringContentSupport.JsonOptions)
           ?? throw new InvalidOperationException($"Draft {draft.Id} contains invalid authoring content.");

    private static QuestionValidationHistoryDto ToValidationHistoryDto(QuestionValidationResult validation)
        => new(
            validation.Id,
            validation.DraftId,
            validation.Status,
            validation.HasErrors,
            validation.HasWarnings,
            validation.IssueCount,
            validation.ValidatedAtUtc,
            validation.Issues
                .OrderBy(x => x.Id)
                .Select(x => new ValidationIssueDto(
                    x.Stage,
                    x.Severity,
                    x.RuleId,
                    x.Message,
                    x.FieldPath,
                    x.Suggestion))
                .ToArray());

    private sealed record PipelineExecution(
        QuestionAuthoringRequest EffectiveRequest,
        string ContentHash,
        ValidateQuestionResponse Validation,
        PreviewQuestionResponse Preview);
}
