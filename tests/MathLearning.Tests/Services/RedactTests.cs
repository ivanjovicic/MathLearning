using System.Text.Json;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Sync;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MathLearning.Tests.Services;

public sealed class RedactTests
{
    [Fact]
    public async Task DeadLetterFailureReason_IsBoundedAndRedacted()
    {
        var db = await TestDbContextFactory.CreateWithSeedAsync();
        var service = CreateSyncService(
            db,
            new SyncOptions
            {
                RequireOperationSignatures = false,
                MaxProcessingRetries = 1,
                MaxInternalDiagnosticLength = 96
            },
            new ThrowingAntiCheatService());

        await service.RegisterDeviceAsync(
            "1",
            new RegisterSyncDeviceRequest("device-redact", "Redact phone", "android", "1.0.0"),
            CancellationToken.None);

        var operation = new SyncOperationDto(
            Guid.NewGuid(),
            "device-redact",
            "1",
            1,
            "submit_answer",
            DateTime.UtcNow,
            JsonSerializer.SerializeToElement(new SubmitAnswerSyncPayloadDto(
                "session-redact",
                1,
                "wrong-answer",
                5,
                DateTime.UtcNow)),
            null);

        await service.SyncAsync("1", new SyncRequestDto("device-redact", 0, [operation]), CancellationToken.None);

        var log = await db.SyncEventLogs.SingleAsync(x => x.OperationId == operation.OperationId);
        var deadLetter = await db.SyncDeadLetters.SingleAsync(x => x.OperationId == operation.OperationId);

        Assert.Equal(SyncEventStatuses.DeadLettered, log.Status);
        Assert.Equal("processing_failed", log.ErrorCode);
        Assert.Equal("Transient processing failure. Retry later.", log.ErrorMessage);
        Assert.DoesNotContain("supersecret", deadLetter.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", deadLetter.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("processing_failed", deadLetter.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(deadLetter.FailureReason.Length <= 96, $"Expected <= 96 chars but got {deadLetter.FailureReason.Length}: {deadLetter.FailureReason}");
    }

    [Fact]
    public void SafeFailureText_TruncatesAndRedactsSecrets()
    {
        var reason = SyncRequestValidation.BuildSafeFailureReason(
            "processing_failed",
            "password=supersecret token=abc123 extra detail that should be trimmed",
            48);

        Assert.DoesNotContain("supersecret", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<redacted>", reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(reason.Length <= 48, $"Expected <= 48 chars but got {reason.Length}: {reason}");
    }

    private static SyncService CreateSyncService(ApiDbContext db, SyncOptions options, IAnswerPatternAntiCheatService antiCheatService)
    {
        var xpTrackingService = new XpTrackingService(
            db,
            Options.Create(new XpTrackingOptions()),
            NullLogger<XpTrackingService>.Instance,
            null);

        return new SyncService(
            db,
            xpTrackingService,
            antiCheatService,
            Options.Create(options),
            new SyncMetricsService(),
            NullLogger<SyncService>.Instance);
    }

    private sealed class ThrowingAntiCheatService : IAnswerPatternAntiCheatService
    {
        private static readonly InvalidOperationException SecretFailure = new("password=supersecret token=abc123 analysis failed");

        public Task<MathLearning.Application.DTOs.AntiCheat.AntiCheatDetectionResultDto> EvaluateAndTrackAsync(
            MathLearning.Application.DTOs.AntiCheat.AntiCheatAnswerObservationInput input,
            CancellationToken cancellationToken = default)
        {
            throw SecretFailure;
        }

        public Task<IReadOnlyList<MathLearning.Application.DTOs.AntiCheat.AntiCheatDetectionResultDto>> EvaluateAndTrackBatchAsync(
            IReadOnlyList<MathLearning.Application.DTOs.AntiCheat.AntiCheatAnswerObservationInput> inputs,
            CancellationToken cancellationToken = default)
        {
            throw SecretFailure;
        }
    }
}
