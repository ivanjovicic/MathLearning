using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Helpers;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MathLearning.Infrastructure.Services.Sync;

public sealed class SyncService : ISyncService, ISyncAdminService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ApiDbContext db;
    private readonly XpTrackingService xpTrackingService;
    private readonly IOptions<SyncOptions> options;
    private readonly SyncMetricsService metrics;
    private readonly ILogger<SyncService> logger;

    public SyncService(
        ApiDbContext db,
        XpTrackingService xpTrackingService,
        IOptions<SyncOptions> options,
        SyncMetricsService metrics,
        ILogger<SyncService> logger)
    {
        this.db = db;
        this.xpTrackingService = xpTrackingService;
        this.options = options;
        this.metrics = metrics;
        this.logger = logger;
    }

    public async Task<RegisterSyncDeviceResponse> RegisterDeviceAsync(
        string userId,
        RegisterSyncDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new InvalidOperationException("DeviceId is required.");
        }

        var existingOtherUser = await db.SyncDevices
            .FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.UserId != userId, cancellationToken);
        if (existingOtherUser is not null)
        {
            throw new InvalidOperationException("DeviceId is already bound to another user.");
        }

        var secret = CreateSecret();
        var device = await db.SyncDevices
            .FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.UserId == userId, cancellationToken);

        if (device is null)
        {
            device = new SyncDevice
            {
                DeviceId = request.DeviceId.Trim(),
                UserId = userId,
                DeviceName = request.DeviceName?.Trim(),
                Platform = NormalizePlatform(request.Platform),
                AppVersion = request.AppVersion?.Trim(),
                SecretKey = secret,
                Status = SyncDeviceStatuses.Active,
                RegisteredAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };
            db.SyncDevices.Add(device);
        }
        else
        {
            device.DeviceName = request.DeviceName?.Trim();
            device.Platform = NormalizePlatform(request.Platform);
            device.AppVersion = request.AppVersion?.Trim();
            device.SecretKey = secret;
            device.Status = SyncDeviceStatuses.Active;
            device.LastSeenAtUtc = DateTime.UtcNow;
        }

        var state = await db.DeviceSyncStates
            .FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.UserId == userId, cancellationToken);
        if (state is null)
        {
            db.DeviceSyncStates.Add(new DeviceSyncState
            {
                DeviceId = request.DeviceId.Trim(),
                UserId = userId,
                LastAcknowledgedEvent = 0,
                LastProcessedClientSequence = 0,
                LastSyncTimeUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new RegisterSyncDeviceResponse(
            request.DeviceId.Trim(),
            secret,
            device.RegisteredAtUtc);
    }

    public async Task<SyncResponseDto> SyncAsync(
        string authenticatedUserId,
        SyncRequestDto request,
        CancellationToken cancellationToken)
    {
        metrics.IncrementSyncRequests();

        if (request.Operations.Count > options.Value.MaxBatchSize)
        {
            throw new InvalidOperationException($"Batch too large. MaxBatchSize={options.Value.MaxBatchSize}.");
        }

        var device = await db.SyncDevices
            .FirstOrDefaultAsync(
                x => x.DeviceId == request.DeviceId &&
                     x.UserId == authenticatedUserId &&
                     x.Status == SyncDeviceStatuses.Active,
                cancellationToken)
            ?? throw new InvalidOperationException("Active sync device not found. Register device first.");

        var deviceState = await db.DeviceSyncStates
            .FirstOrDefaultAsync(x => x.DeviceId == request.DeviceId && x.UserId == authenticatedUserId, cancellationToken);
        if (deviceState is null)
        {
            deviceState = new DeviceSyncState
            {
                DeviceId = request.DeviceId,
                UserId = authenticatedUserId,
                LastAcknowledgedEvent = 0,
                LastProcessedClientSequence = 0,
                LastSyncTimeUtc = DateTime.UtcNow
            };
            db.DeviceSyncStates.Add(deviceState);
        }

        device.LastSeenAtUtc = DateTime.UtcNow;
        deviceState.LastBundleVersion = request.BundleVersion ?? deviceState.LastBundleVersion;

        var orderedOperations = request.Operations
            .OrderBy(x => x.ClientSequence)
            .ThenBy(x => x.OccurredAtUtc)
            .ToList();

        var operationIds = orderedOperations.Select(x => x.OperationId).Distinct().ToList();
        var existingLogs = await db.SyncEventLogs
            .Where(x => operationIds.Contains(x.OperationId))
            .ToDictionaryAsync(x => x.OperationId, cancellationToken);

        var acknowledgements = new List<SyncOperationAckDto>(orderedOperations.Count);
        var expectedSequence = deviceState.LastProcessedClientSequence + 1;
        var stopFurtherProcessing = false;
        var useTransaction = db.Database.IsRelational();

        await using var tx = useTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        foreach (var operation in orderedOperations)
        {
            if (stopFurtherProcessing)
            {
                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    operation.ClientSequence,
                    "Deferred",
                    "sequence_gap",
                    "Previous operation sequence is missing."));
                continue;
            }

            if (existingLogs.TryGetValue(operation.OperationId, out var existing))
            {
                if (existing.Status == SyncEventStatuses.Failed &&
                    existing.ClientSequence == expectedSequence)
                {
                    var retryAck = await RetryFailedOperationAsync(existing, authenticatedUserId, device, deviceState, cancellationToken);
                    acknowledgements.Add(retryAck);
                    if (retryAck.Status is "Accepted" or "Rejected" or "DeadLettered")
                    {
                        expectedSequence = deviceState.LastProcessedClientSequence + 1;
                    }
                    else
                    {
                        stopFurtherProcessing = true;
                    }

                    continue;
                }

                metrics.IncrementDuplicate();
                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    existing.ClientSequence,
                    existing.Status == SyncEventStatuses.Processed ? "Duplicate" : existing.Status,
                    existing.ErrorCode,
                    existing.ErrorMessage));
                continue;
            }

            if (operation.ClientSequence < expectedSequence)
            {
                metrics.IncrementRejected("sequence_conflict");
                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    operation.ClientSequence,
                    "Rejected",
                    "sequence_conflict",
                    "Client sequence is older than the server cursor."));
                continue;
            }

            if (operation.ClientSequence > expectedSequence)
            {
                metrics.IncrementRejected("sequence_gap");
                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    operation.ClientSequence,
                    "Deferred",
                    "sequence_gap",
                    "Missing earlier client sequence."));
                stopFurtherProcessing = true;
                continue;
            }

            var log = new SyncEventLog
            {
                OperationId = operation.OperationId,
                DeviceId = operation.DeviceId,
                UserId = operation.UserId,
                ClientSequence = operation.ClientSequence,
                OperationType = operation.OperationType.Trim(),
                PayloadJson = operation.Payload.GetRawText(),
                Status = SyncEventStatuses.Received,
                OccurredAtUtc = operation.OccurredAtUtc,
                ReceivedAtUtc = DateTime.UtcNow
            };
            db.SyncEventLogs.Add(log);

            try
            {
                await ValidateOperationEnvelopeAsync(authenticatedUserId, request.DeviceId, device, operation, cancellationToken);
                await ProcessOperationAsync(log, cancellationToken);
                log.Status = SyncEventStatuses.Processed;
                log.ProcessedAtUtc = DateTime.UtcNow;
                log.ErrorCode = null;
                log.ErrorMessage = null;
                deviceState.LastProcessedClientSequence = operation.ClientSequence;
                expectedSequence = deviceState.LastProcessedClientSequence + 1;
                metrics.IncrementProcessed();

                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    operation.ClientSequence,
                    "Accepted"));
            }
            catch (SyncOperationRejectedException ex)
            {
                log.Status = SyncEventStatuses.Rejected;
                log.ProcessedAtUtc = DateTime.UtcNow;
                log.ErrorCode = ex.Code;
                log.ErrorMessage = ex.Message;
                deviceState.LastProcessedClientSequence = operation.ClientSequence;
                expectedSequence = deviceState.LastProcessedClientSequence + 1;
                metrics.IncrementRejected(ex.Code);

                acknowledgements.Add(new SyncOperationAckDto(
                    operation.OperationId,
                    operation.ClientSequence,
                    "Rejected",
                    ex.Code,
                    ex.Message));
            }
            catch (Exception ex)
            {
                log.RetryCount++;
                log.ErrorCode = "processing_failed";
                log.ErrorMessage = ex.Message;

                if (log.RetryCount >= options.Value.MaxProcessingRetries)
                {
                    log.Status = SyncEventStatuses.DeadLettered;
                    log.ProcessedAtUtc = DateTime.UtcNow;
                    db.SyncDeadLetters.Add(new SyncDeadLetter
                    {
                        SyncEventLogId = log.Id == 0 ? null : log.Id,
                        OperationId = log.OperationId,
                        DeviceId = log.DeviceId,
                        UserId = log.UserId,
                        OperationType = log.OperationType,
                        PayloadJson = log.PayloadJson,
                        RetryCount = log.RetryCount,
                        Status = SyncDeadLetterStatuses.Pending,
                        FailureReason = ex.ToString(),
                        CreatedAtUtc = DateTime.UtcNow,
                        LastFailedAtUtc = DateTime.UtcNow
                    });
                    deviceState.LastProcessedClientSequence = operation.ClientSequence;
                    expectedSequence = deviceState.LastProcessedClientSequence + 1;
                    metrics.IncrementDeadLetter("processing_failed");

                    acknowledgements.Add(new SyncOperationAckDto(
                        operation.OperationId,
                        operation.ClientSequence,
                        "DeadLettered",
                        "processing_failed",
                        "Operation moved to dead-letter queue."));
                }
                else
                {
                    log.Status = SyncEventStatuses.Failed;
                    metrics.IncrementFailed("processing_failed");
                    acknowledgements.Add(new SyncOperationAckDto(
                        operation.OperationId,
                        operation.ClientSequence,
                        "Failed",
                        "processing_failed",
                        "Transient processing failure. Retry later."));
                    stopFurtherProcessing = true;
                }

                logger.LogError(ex, "Sync operation failed. OperationId={OperationId} DeviceId={DeviceId}", operation.OperationId, operation.DeviceId);
            }

            existingLogs[operation.OperationId] = log;
        }

        await db.SaveChangesAsync(cancellationToken);

        var serverEvents = await db.ServerSyncEvents
            .AsNoTracking()
            .Where(x => x.UserId == authenticatedUserId && x.Id > request.LastKnownServerEvent)
            .OrderBy(x => x.Id)
            .Take(options.Value.MaxServerEventsPerSync)
            .ToListAsync(cancellationToken);

        var responseEvents = serverEvents
            .Select(x => new SyncServerEventDto(
                x.Id,
                x.EventType,
                x.AggregateType,
                x.AggregateId,
                x.SourceOperationId,
                JsonDocument.Parse(x.PayloadJson).RootElement.Clone(),
                x.CreatedAtUtc))
            .ToList();

        var newCheckpoint = responseEvents.Count == 0
            ? request.LastKnownServerEvent
            : responseEvents[^1].Id;

        deviceState.LastAcknowledgedEvent = Math.Max(deviceState.LastAcknowledgedEvent, newCheckpoint);
        deviceState.LastSyncTimeUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return new SyncResponseDto(
            acknowledgements,
            responseEvents,
            newCheckpoint,
            stopFurtherProcessing ? 5 : 0);
    }

    public async Task<SyncAdminOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var sinceUtc = DateTime.UtcNow.AddHours(-24);

        var activeDevices24h = await db.SyncDevices
            .AsNoTracking()
            .LongCountAsync(x => x.LastSeenAtUtc >= sinceUtc && x.Status == SyncDeviceStatuses.Active, cancellationToken);

        var pendingDeadLetters = await db.SyncDeadLetters
            .AsNoTracking()
            .LongCountAsync(
                x => x.Status == SyncDeadLetterStatuses.Pending ||
                     x.Status == SyncDeadLetterStatuses.Reprocessing,
                cancellationToken);

        var exhaustedDeadLetters = await db.SyncDeadLetters
            .AsNoTracking()
            .LongCountAsync(x => x.Status == SyncDeadLetterStatuses.Exhausted, cancellationToken);

        var failedOperationsInLog = await db.SyncEventLogs
            .AsNoTracking()
            .LongCountAsync(
                x => x.Status == SyncEventStatuses.Failed ||
                     x.Status == SyncEventStatuses.DeadLettered,
                cancellationToken);

        var latestServerEventId = await db.ServerSyncEvents
            .AsNoTracking()
            .Select(x => (long?)x.Id)
            .MaxAsync(cancellationToken) ?? 0;

        return new SyncAdminOverviewDto(
            activeDevices24h,
            pendingDeadLetters,
            exhaustedDeadLetters,
            failedOperationsInLog,
            latestServerEventId,
            metrics.Snapshot());
    }

    public async Task<IReadOnlyList<SyncDeadLetterItemDto>> GetDeadLettersAsync(
        int take,
        string? status,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);

        var query = db.SyncDeadLetters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }

        return await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.LastFailedAtUtc)
            .Take(take)
            .Select(x => new SyncDeadLetterItemDto(
                x.OperationId,
                x.SyncEventLogId,
                x.DeviceId,
                x.UserId,
                x.OperationType,
                x.Status,
                x.RetryCount,
                x.FailureReason,
                x.CreatedAtUtc,
                x.LastFailedAtUtc,
                x.LastRedriveAttemptAtUtc,
                x.ResolvedAtUtc,
                x.ResolutionNote))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncDeviceAdminDto>> GetDevicesAsync(
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);

        return await (
                from device in db.SyncDevices.AsNoTracking()
                join state in db.DeviceSyncStates.AsNoTracking()
                    on new { device.DeviceId, device.UserId } equals new { state.DeviceId, state.UserId } into stateJoin
                from state in stateJoin.DefaultIfEmpty()
                orderby device.LastSeenAtUtc descending
                select new SyncDeviceAdminDto(
                    device.DeviceId,
                    device.UserId,
                    device.DeviceName,
                    device.Platform,
                    device.AppVersion,
                    device.Status,
                    device.LastSeenAtUtc,
                    state != null ? state.LastSyncTimeUtc : null,
                    state != null ? state.LastProcessedClientSequence : 0,
                    state != null ? state.LastAcknowledgedEvent : 0,
                    state != null ? state.LastBundleVersion : null))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<SyncDeadLetterRedriveResponseDto> RedriveDeadLetterAsync(
        Guid operationId,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var deadLetter = await db.SyncDeadLetters
            .FirstOrDefaultAsync(x => x.OperationId == operationId, cancellationToken)
            ?? throw new InvalidOperationException($"Dead-letter operation '{operationId}' was not found.");

        return await RedriveDeadLetterInternalAsync(deadLetter, actorUserId, cancellationToken);
    }

    public async Task<SyncDeadLetterRedriveBatchResponseDto> RedriveDeadLettersAsync(
        int? take,
        bool includeExhausted,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var effectiveTake = Math.Clamp(take ?? options.Value.DeadLetterRedriveBatchSize, 1, 200);
        var eligibleStatuses = includeExhausted
            ? new[] { SyncDeadLetterStatuses.Pending, SyncDeadLetterStatuses.Exhausted }
            : new[] { SyncDeadLetterStatuses.Pending };

        var deadLetters = await db.SyncDeadLetters
            .Where(x => eligibleStatuses.Contains(x.Status))
            .OrderBy(x => x.LastFailedAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(effectiveTake)
            .ToListAsync(cancellationToken);

        var results = new List<SyncDeadLetterRedriveResponseDto>(deadLetters.Count);
        foreach (var deadLetter in deadLetters)
        {
            results.Add(await RedriveDeadLetterInternalAsync(deadLetter, actorUserId, cancellationToken));
        }

        return new SyncDeadLetterRedriveBatchResponseDto(
            results.Count,
            results.Count(x => x.Status == SyncDeadLetterStatuses.Resolved),
            results.Count(x => x.Status == SyncDeadLetterStatuses.Pending),
            results.Count(x => x.Status == SyncDeadLetterStatuses.Exhausted),
            results);
    }

    public static string BuildOperationSignature(string secret, SyncOperationDto operation)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var canonical = $"{operation.OperationId:N}|{operation.DeviceId}|{operation.UserId}|{operation.ClientSequence}|{operation.OperationType}|{operation.OccurredAtUtc:O}|{operation.Payload.GetRawText()}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(hash);
    }

    private async Task<SyncOperationAckDto> RetryFailedOperationAsync(
        SyncEventLog log,
        string authenticatedUserId,
        SyncDevice device,
        DeviceSyncState deviceState,
        CancellationToken cancellationToken)
    {
        var payload = JsonDocument.Parse(log.PayloadJson).RootElement;
        var operation = new SyncOperationDto(
            log.OperationId,
            log.DeviceId,
            log.UserId,
            log.ClientSequence,
            log.OperationType,
            log.OccurredAtUtc,
            payload,
            null);

        try
        {
            await ValidateOperationEnvelopeAsync(authenticatedUserId, device.DeviceId, device, operation, cancellationToken, skipSignatureValidation: true);
            await ProcessOperationAsync(log, cancellationToken);
            log.Status = SyncEventStatuses.Processed;
            log.ProcessedAtUtc = DateTime.UtcNow;
            log.ErrorCode = null;
            log.ErrorMessage = null;
            deviceState.LastProcessedClientSequence = log.ClientSequence;
            metrics.IncrementProcessed();

            return new SyncOperationAckDto(log.OperationId, log.ClientSequence, "Accepted");
        }
        catch (SyncOperationRejectedException ex)
        {
            log.Status = SyncEventStatuses.Rejected;
            log.ProcessedAtUtc = DateTime.UtcNow;
            log.ErrorCode = ex.Code;
            log.ErrorMessage = ex.Message;
            deviceState.LastProcessedClientSequence = log.ClientSequence;
            metrics.IncrementRejected(ex.Code);

            return new SyncOperationAckDto(log.OperationId, log.ClientSequence, "Rejected", ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            log.RetryCount++;
            log.Status = log.RetryCount >= options.Value.MaxProcessingRetries
                ? SyncEventStatuses.DeadLettered
                : SyncEventStatuses.Failed;
            log.ProcessedAtUtc = log.Status == SyncEventStatuses.DeadLettered ? DateTime.UtcNow : null;
            log.ErrorCode = "processing_failed";
            log.ErrorMessage = ex.Message;

            if (log.Status == SyncEventStatuses.DeadLettered)
            {
                db.SyncDeadLetters.Add(new SyncDeadLetter
                {
                    SyncEventLogId = log.Id,
                    OperationId = log.OperationId,
                    DeviceId = log.DeviceId,
                    UserId = log.UserId,
                    OperationType = log.OperationType,
                    PayloadJson = log.PayloadJson,
                    RetryCount = log.RetryCount,
                    Status = SyncDeadLetterStatuses.Pending,
                    FailureReason = ex.ToString(),
                    CreatedAtUtc = DateTime.UtcNow,
                    LastFailedAtUtc = DateTime.UtcNow
                });
                deviceState.LastProcessedClientSequence = log.ClientSequence;
                metrics.IncrementDeadLetter("processing_failed");
                return new SyncOperationAckDto(log.OperationId, log.ClientSequence, "DeadLettered", "processing_failed", "Operation moved to dead-letter queue.");
            }

            metrics.IncrementFailed("processing_failed");
            return new SyncOperationAckDto(log.OperationId, log.ClientSequence, "Failed", "processing_failed", "Transient processing failure. Retry later.");
        }
    }

    private async Task ValidateOperationEnvelopeAsync(
        string authenticatedUserId,
        string requestDeviceId,
        SyncDevice device,
        SyncOperationDto operation,
        CancellationToken cancellationToken,
        bool skipSignatureValidation = false)
    {
        if (!string.Equals(operation.UserId, authenticatedUserId, StringComparison.Ordinal))
        {
            throw new SyncOperationRejectedException("user_mismatch", "Operation user does not match authenticated user.");
        }

        if (!string.Equals(operation.DeviceId, requestDeviceId, StringComparison.Ordinal))
        {
            throw new SyncOperationRejectedException("device_mismatch", "Operation device does not match request device.");
        }

        if (operation.ClientSequence <= 0)
        {
            throw new SyncOperationRejectedException("invalid_sequence", "ClientSequence must be greater than zero.");
        }

        if (!skipSignatureValidation && options.Value.RequireOperationSignatures)
        {
            if (string.IsNullOrWhiteSpace(operation.Signature))
            {
                throw new SyncOperationRejectedException("missing_signature", "Operation signature is required.");
            }

            var expectedSignature = BuildOperationSignature(device.SecretKey, operation);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(operation.Signature.Trim())))
            {
                throw new SyncOperationRejectedException("invalid_signature", "Operation signature is invalid.");
            }
        }

        await EnsureUserProfileAsync(authenticatedUserId, cancellationToken);
    }

    private async Task ProcessOperationAsync(SyncEventLog log, CancellationToken cancellationToken)
    {
        switch (log.OperationType.Trim().ToLowerInvariant())
        {
            case "submit_answer":
                await ProcessSubmitAnswerAsync(log, cancellationToken);
                return;
            default:
                throw new SyncOperationRejectedException("unsupported_operation", $"Unsupported operation type '{log.OperationType}'.");
        }
    }

    private async Task ProcessSubmitAnswerAsync(SyncEventLog log, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<SubmitAnswerSyncPayloadDto>(log.PayloadJson, SerializerOptions)
            ?? throw new SyncOperationRejectedException("invalid_payload", "Submit answer payload is invalid.");

        if (payload.QuestionId <= 0 || string.IsNullOrWhiteSpace(payload.Answer) || string.IsNullOrWhiteSpace(payload.SessionId))
        {
            throw new SyncOperationRejectedException("invalid_payload", "Submit answer payload is incomplete.");
        }

        var question = await db.Questions
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == payload.QuestionId, cancellationToken)
            ?? throw new SyncOperationRejectedException("question_not_found", $"Question '{payload.QuestionId}' was not found.");

        var sessionId = ResolveSessionId(log.UserId, log.DeviceId, payload.SessionId);
        var session = await db.QuizSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == log.UserId, cancellationToken);
        if (session is null)
        {
            session = new QuizSession
            {
                Id = sessionId,
                UserId = log.UserId,
                StartedAt = payload.AnsweredAtUtc
            };
            db.QuizSessions.Add(session);
        }

        var existingAnswer = await db.UserAnswers
            .FirstOrDefaultAsync(x => x.SyncOperationId == log.OperationId, cancellationToken);
        if (existingAnswer is not null)
        {
            return;
        }

        var isCorrect = question.Type == "multiple_choice"
            ? question.Options.Any(o =>
                o.IsCorrect &&
                (o.Text == payload.Answer ||
                 (int.TryParse(payload.Answer, out var selectedOptionId) && o.Id == selectedOptionId)))
            : question.CorrectAnswer != null &&
              question.CorrectAnswer.Trim().Equals(payload.Answer.Trim(), StringComparison.OrdinalIgnoreCase);

        db.UserAnswers.Add(new UserAnswer
        {
            SyncOperationId = log.OperationId,
            DeviceId = log.DeviceId,
            ClientSequence = log.ClientSequence,
            UserId = log.UserId,
            QuestionId = payload.QuestionId,
            QuizSessionId = sessionId,
            Answer = payload.Answer,
            IsCorrect = isCorrect,
            TimeSpentSeconds = Math.Max(0, payload.TimeSpentSeconds),
            AnsweredAt = payload.AnsweredAtUtc
        });

        var profile = await EnsureUserProfileAsync(log.UserId, cancellationToken);
        profile.LastActivityDay = DateOnly.FromDateTime(payload.AnsweredAtUtc);
        profile.UpdatedAt = DateTime.UtcNow;

        var userQuestionStat = await db.UserQuestionStats
            .FirstOrDefaultAsync(x => x.UserId == log.UserId && x.QuestionId == payload.QuestionId, cancellationToken);
        if (userQuestionStat is null)
        {
            userQuestionStat = new UserQuestionStat
            {
                UserId = log.UserId,
                QuestionId = payload.QuestionId
            };
            db.UserQuestionStats.Add(userQuestionStat);
        }

        userQuestionStat.Attempts++;
        if (isCorrect)
        {
            userQuestionStat.CorrectAttempts++;
        }

        if (userQuestionStat.LastAttemptAt is null || payload.AnsweredAtUtc > userQuestionStat.LastAttemptAt.Value)
        {
            userQuestionStat.LastAttemptAt = payload.AnsweredAtUtc;
        }

        var questionStat = await db.QuestionStats
            .FirstOrDefaultAsync(x => x.UserId == log.UserId && x.QuestionId == payload.QuestionId, cancellationToken);
        if (questionStat is null)
        {
            questionStat = new QuestionStat
            {
                UserId = log.UserId,
                QuestionId = payload.QuestionId
            };
            db.QuestionStats.Add(questionStat);
        }

        ApplySpacedRepetition(questionStat, isCorrect, payload.AnsweredAtUtc);
        await UpdateAnalyticsProjectionAsync(log.UserId, question.SubtopicId, sessionId, payload.QuestionId, isCorrect, payload.TimeSpentSeconds, payload.AnsweredAtUtc, cancellationToken);
        await UpdateDailyStatsAndStreakAsync(profile, payload.AnsweredAtUtc, cancellationToken);

        if (isCorrect)
        {
            await xpTrackingService.AddXpAsync(
                log.UserId,
                10,
                "sync_submit_answer",
                log.OperationId.ToString(),
                null,
                cancellationToken);
        }

        db.ServerSyncEvents.Add(new ServerSyncEvent
        {
            UserId = log.UserId,
            DeviceId = log.DeviceId,
            EventType = "answer_processed",
            AggregateType = "question",
            AggregateId = payload.QuestionId.ToString(),
            SourceOperationId = log.OperationId,
            PayloadJson = JsonSerializer.Serialize(new
            {
                questionId = payload.QuestionId,
                sessionId,
                isCorrect,
                answeredAtUtc = payload.AnsweredAtUtc,
                attempts = userQuestionStat.Attempts,
                correctAttempts = userQuestionStat.CorrectAttempts,
                nextReviewAtUtc = questionStat.NextReview,
                streak = profile.Streak,
                xp = profile.Xp,
                level = profile.Level
            }, SerializerOptions),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task UpdateAnalyticsProjectionAsync(
        string userId,
        int subtopicId,
        Guid quizId,
        int questionId,
        bool correct,
        int timeSpentSeconds,
        DateTime answeredAtUtc,
        CancellationToken cancellationToken)
    {
        var analyticsUserId = UserIdGuidMapper.FromIdentityUserId(userId);
        var subtopic = await db.Subtopics.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subtopicId, cancellationToken);
        if (subtopic is null)
        {
            return;
        }

        db.QuizAttempts.Add(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = analyticsUserId,
            QuizId = quizId,
            QuestionId = questionId,
            TopicId = subtopic.TopicId,
            SubtopicId = subtopicId,
            Correct = correct,
            TimeSpentMs = Math.Max(0, timeSpentSeconds) * 1000,
            CreatedAt = answeredAtUtc
        });

        var topicStat = await db.UserTopicStats
            .FirstOrDefaultAsync(x => x.UserId == analyticsUserId && x.TopicId == subtopic.TopicId, cancellationToken);
        if (topicStat is null)
        {
            topicStat = new UserTopicStat
            {
                UserId = analyticsUserId,
                TopicId = subtopic.TopicId,
                TotalQuestions = 0,
                CorrectAnswers = 0,
                Accuracy = 0m,
                LastAttempt = answeredAtUtc,
                WeaknessScore = 0m
            };
            db.UserTopicStats.Add(topicStat);
        }

        topicStat.TotalQuestions += 1;
        topicStat.CorrectAnswers += correct ? 1 : 0;
        topicStat.Accuracy = CalculateAccuracy(topicStat.CorrectAnswers, topicStat.TotalQuestions);
        topicStat.LastAttempt = topicStat.LastAttempt >= answeredAtUtc ? topicStat.LastAttempt : answeredAtUtc;
        topicStat.WeaknessScore = CalculateWeaknessScore(
            topicStat.Accuracy,
            topicStat.TotalQuestions,
            CalculateRecencyFactor(topicStat.LastAttempt, DateTime.UtcNow));

        var subtopicStat = await db.UserSubtopicStats
            .FirstOrDefaultAsync(x => x.UserId == analyticsUserId && x.SubtopicId == subtopicId, cancellationToken);
        if (subtopicStat is null)
        {
            subtopicStat = new UserSubtopicStat
            {
                UserId = analyticsUserId,
                SubtopicId = subtopicId,
                TotalQuestions = 0,
                CorrectAnswers = 0,
                Accuracy = 0m,
                LastAttempt = answeredAtUtc,
                WeaknessScore = 0m
            };
            db.UserSubtopicStats.Add(subtopicStat);
        }

        subtopicStat.TotalQuestions += 1;
        subtopicStat.CorrectAnswers += correct ? 1 : 0;
        subtopicStat.Accuracy = CalculateAccuracy(subtopicStat.CorrectAnswers, subtopicStat.TotalQuestions);
        subtopicStat.LastAttempt = subtopicStat.LastAttempt >= answeredAtUtc ? subtopicStat.LastAttempt : answeredAtUtc;
        subtopicStat.WeaknessScore = CalculateWeaknessScore(
            subtopicStat.Accuracy,
            subtopicStat.TotalQuestions,
            CalculateRecencyFactor(subtopicStat.LastAttempt, DateTime.UtcNow));
    }

    private async Task UpdateDailyStatsAndStreakAsync(
        UserProfile profile,
        DateTime answeredAtUtc,
        CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(answeredAtUtc);
        var dailyStat = await db.UserDailyStats
            .FirstOrDefaultAsync(x => x.UserId == profile.UserId && x.Day == day, cancellationToken);
        if (dailyStat is null)
        {
            dailyStat = new UserDailyStat
            {
                UserId = profile.UserId,
                Day = day,
                Completed = false
            };
            db.UserDailyStats.Add(dailyStat);
        }

        var solvedCount = await db.UserAnswers
            .CountAsync(x => x.UserId == profile.UserId && DateOnly.FromDateTime(x.AnsweredAt) == day, cancellationToken);

        if (solvedCount + 1 >= 5)
        {
            dailyStat.Completed = true;
        }

        var completedDays = await db.UserDailyStats
            .AsNoTracking()
            .Where(x => x.UserId == profile.UserId && x.Completed)
            .OrderByDescending(x => x.Day)
            .Select(x => x.Day)
            .ToListAsync(cancellationToken);

        if (dailyStat.Completed && !completedDays.Contains(day))
        {
            completedDays.Add(day);
            completedDays = completedDays.OrderByDescending(x => x).ToList();
        }

        if (completedDays.Count == 0)
        {
            profile.Streak = 0;
            return;
        }

        var streak = 1;
        var cursor = completedDays[0];
        for (var i = 1; i < completedDays.Count; i++)
        {
            if (completedDays[i] != cursor.AddDays(-1))
            {
                break;
            }

            streak++;
            cursor = completedDays[i];
        }

        profile.Streak = streak;
        profile.LastStreakDay = completedDays[0];
    }

    private static void ApplySpacedRepetition(QuestionStat questionStat, bool isCorrect, DateTime answeredAtUtc)
    {
        var baseIntervals = new[] { 1d, 2d, 4d, 7d, 15d };

        if (isCorrect)
        {
            questionStat.SuccessStreak++;
            questionStat.Ease = Math.Min(3.0, questionStat.Ease + 0.05);
        }
        else
        {
            questionStat.SuccessStreak = 0;
            questionStat.Ease = Math.Max(1.0, questionStat.Ease - 0.1);
        }

        var intervalIndex = Math.Min(questionStat.SuccessStreak, baseIntervals.Length - 1);
        var intervalDays = baseIntervals[intervalIndex] * questionStat.Ease;
        questionStat.LastAnswered = answeredAtUtc;
        questionStat.NextReview = answeredAtUtc.AddDays(intervalDays);
    }

    private async Task<UserProfile> EnsureUserProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new UserProfile
        {
            UserId = userId,
            Username = userId,
            DisplayName = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.UserProfiles.Add(profile);
        return profile;
    }

    private static Guid ResolveSessionId(string userId, string deviceId, string rawSessionId)
    {
        if (Guid.TryParse(rawSessionId, out var parsed))
        {
            return parsed;
        }

        var source = $"{userId}:{deviceId}:{rawSessionId}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var guidBytes = new byte[16];
        Array.Copy(bytes, guidBytes, guidBytes.Length);
        return new Guid(guidBytes);
    }

    private async Task<SyncDeadLetterRedriveResponseDto> RedriveDeadLetterInternalAsync(
        SyncDeadLetter deadLetter,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        deadLetter.Status = SyncDeadLetterStatuses.Reprocessing;
        deadLetter.LastRedriveAttemptAtUtc = now;

        var log = await db.SyncEventLogs
            .FirstOrDefaultAsync(x => x.OperationId == deadLetter.OperationId, cancellationToken);
        if (log is null)
        {
            deadLetter.Status = SyncDeadLetterStatuses.Exhausted;
            deadLetter.LastFailedAtUtc = now;
            deadLetter.ResolutionNote = BuildResolutionNote(actorUserId, "Event log was not found.");
            await db.SaveChangesAsync(cancellationToken);

            return new SyncDeadLetterRedriveResponseDto(
                deadLetter.OperationId,
                deadLetter.Status,
                deadLetter.RetryCount,
                "event_log_missing",
                "Sync event log was not found.",
                deadLetter.ResolvedAtUtc);
        }

        var device = await db.SyncDevices
            .FirstOrDefaultAsync(
                x => x.DeviceId == log.DeviceId &&
                     x.UserId == log.UserId &&
                     x.Status == SyncDeviceStatuses.Active,
                cancellationToken);
        if (device is null)
        {
            deadLetter.RetryCount++;
            deadLetter.Status = SyncDeadLetterStatuses.Exhausted;
            deadLetter.LastFailedAtUtc = now;
            deadLetter.FailureReason = "Active sync device was not found.";
            deadLetter.ResolutionNote = BuildResolutionNote(actorUserId, "Device missing or revoked.");

            log.Status = SyncEventStatuses.Rejected;
            log.ProcessedAtUtc = now;
            log.ErrorCode = "device_missing";
            log.ErrorMessage = "Active sync device not found.";

            await db.SaveChangesAsync(cancellationToken);

            return new SyncDeadLetterRedriveResponseDto(
                deadLetter.OperationId,
                deadLetter.Status,
                deadLetter.RetryCount,
                "device_missing",
                "Active sync device not found.",
                deadLetter.ResolvedAtUtc);
        }

        var payload = JsonDocument.Parse(log.PayloadJson).RootElement;
        var operation = new SyncOperationDto(
            log.OperationId,
            log.DeviceId,
            log.UserId,
            log.ClientSequence,
            log.OperationType,
            log.OccurredAtUtc,
            payload,
            null);

        try
        {
            await ValidateOperationEnvelopeAsync(log.UserId, log.DeviceId, device, operation, cancellationToken, skipSignatureValidation: true);
            await ProcessOperationAsync(log, cancellationToken);

            log.Status = SyncEventStatuses.Processed;
            log.ProcessedAtUtc = now;
            log.ErrorCode = null;
            log.ErrorMessage = null;

            deadLetter.Status = SyncDeadLetterStatuses.Resolved;
            deadLetter.ResolvedAtUtc = now;
            deadLetter.ResolutionNote = BuildResolutionNote(actorUserId, "Redrive succeeded.");

            await db.SaveChangesAsync(cancellationToken);

            return new SyncDeadLetterRedriveResponseDto(
                deadLetter.OperationId,
                deadLetter.Status,
                deadLetter.RetryCount,
                null,
                "Redrive succeeded.",
                deadLetter.ResolvedAtUtc);
        }
        catch (SyncOperationRejectedException ex)
        {
            deadLetter.RetryCount++;
            deadLetter.Status = SyncDeadLetterStatuses.Exhausted;
            deadLetter.LastFailedAtUtc = now;
            deadLetter.FailureReason = ex.Message;
            deadLetter.ResolutionNote = BuildResolutionNote(actorUserId, $"Rejected: {ex.Code}");

            log.Status = SyncEventStatuses.Rejected;
            log.ProcessedAtUtc = now;
            log.ErrorCode = ex.Code;
            log.ErrorMessage = ex.Message;

            metrics.IncrementRejected(ex.Code);
            await db.SaveChangesAsync(cancellationToken);

            return new SyncDeadLetterRedriveResponseDto(
                deadLetter.OperationId,
                deadLetter.Status,
                deadLetter.RetryCount,
                ex.Code,
                ex.Message,
                deadLetter.ResolvedAtUtc);
        }
        catch (Exception ex)
        {
            deadLetter.RetryCount++;
            deadLetter.LastFailedAtUtc = now;
            deadLetter.FailureReason = ex.ToString();
            deadLetter.Status = deadLetter.RetryCount >= options.Value.MaxDeadLetterRedriveAttempts
                ? SyncDeadLetterStatuses.Exhausted
                : SyncDeadLetterStatuses.Pending;
            deadLetter.ResolutionNote = BuildResolutionNote(actorUserId, "Redrive failed.");

            log.RetryCount++;
            log.Status = deadLetter.Status == SyncDeadLetterStatuses.Exhausted
                ? SyncEventStatuses.DeadLettered
                : SyncEventStatuses.Failed;
            log.ErrorCode = "redrive_failed";
            log.ErrorMessage = ex.Message;

            if (deadLetter.Status == SyncDeadLetterStatuses.Exhausted)
            {
                metrics.IncrementDeadLetter("redrive_failed");
            }
            else
            {
                metrics.IncrementFailed("redrive_failed");
            }

            logger.LogError(ex, "Dead-letter redrive failed. OperationId={OperationId}", deadLetter.OperationId);
            await db.SaveChangesAsync(cancellationToken);

            return new SyncDeadLetterRedriveResponseDto(
                deadLetter.OperationId,
                deadLetter.Status,
                deadLetter.RetryCount,
                "redrive_failed",
                ex.Message,
                deadLetter.ResolvedAtUtc);
        }
    }

    private static string CreateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static decimal CalculateAccuracy(int correctAnswers, int totalQuestions)
    {
        if (totalQuestions <= 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)correctAnswers / totalQuestions, 4, MidpointRounding.AwayFromZero);
    }

    private static double CalculateRecencyFactor(DateTime? lastAttemptUtc, DateTime nowUtc)
    {
        if (lastAttemptUtc is null)
        {
            return 0d;
        }

        var days = Math.Max(0d, (nowUtc - lastAttemptUtc.Value).TotalDays);
        return Math.Exp(-days / 14d);
    }

    private static decimal CalculateWeaknessScore(decimal accuracy, int totalQuestions, double recencyFactor)
    {
        var attemptFactor = Math.Clamp(Math.Log10(totalQuestions + 1d), 0d, 1.5d);
        var weakness = (1d - (double)accuracy) * attemptFactor * (0.5d + (0.5d * recencyFactor));
        return decimal.Round((decimal)Math.Clamp(weakness, 0d, 3d), 4, MidpointRounding.AwayFromZero);
    }

    private static string? BuildResolutionNote(string? actorUserId, string message)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return message;
        }

        return $"{message} actor={actorUserId}";
    }

    private static string NormalizePlatform(string platform) =>
        string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim().ToLowerInvariant();

    private sealed class SyncOperationRejectedException : Exception
    {
        public SyncOperationRejectedException(string code, string message) : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
