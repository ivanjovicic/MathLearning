using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Services;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MathLearning.Tests.Services;

public sealed class BugScreenshotStorageTests
{
    [Fact]
    public async Task CreateBugReport_WhenSaveFails_DeletesUploadedScreenshot()
    {
        var interceptor = new ThrowingSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase($"bug-screenshot-save-fail-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new ApiDbContext(options);
        var storage = new TrackingScreenshotStorageService();
        var service = new BugReportService(db, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateBugReportAsync("learner-1", CreateReportRequest()));

        Assert.Equal(1, storage.UploadCalls);
        Assert.Equal(1, storage.DeleteCalls);
        Assert.False(string.IsNullOrWhiteSpace(storage.UploadedKey));
        Assert.Equal(storage.UploadedKey, storage.DeletedKey);
        Assert.False(storage.Exists(storage.UploadedKey!));
        Assert.Empty(await db.BugReports.ToListAsync());
    }

    [Fact]
    public async Task LocalStorage_RejectsTraversalAndMissingKeys()
    {
        var storage = new LocalScreenshotStorageService();

        Assert.Null(await storage.GetScreenshotAsync("../evil"));
        Assert.False(await storage.DeleteScreenshotAsync("../evil"));
        Assert.Null(await storage.GetScreenshotAsync($"missing-{Guid.NewGuid():N}"));
    }

    private static BugReportRequest CreateReportRequest() => new(
        Screen: "quiz",
        Description: "Storage cleanup should happen on save failure.",
        StepsToReproduce: "Upload a screenshot and force the save to throw.",
        Severity: "medium",
        Platform: "android",
        Locale: "sr-RS",
        AppVersion: "1.0.0",
        ScreenshotBase64: $"data:image/png;base64,{Convert.ToBase64String(MinimalPng)}");

    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result) =>
            throw new InvalidOperationException("simulated save failure");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            new ValueTask<InterceptionResult<int>>(Task.FromException<InterceptionResult<int>>(new InvalidOperationException("simulated save failure")));
    }

    private sealed class TrackingScreenshotStorageService : IScreenshotStorageService
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"bug-screenshot-test-{Guid.NewGuid():N}");

        public int UploadCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public string? UploadedKey { get; private set; }
        public string? DeletedKey { get; private set; }

        public async Task<bool> UploadScreenshotAsync(string base64Data, string storageKey)
        {
            UploadCalls++;
            UploadedKey = storageKey;
            Directory.CreateDirectory(root);
            var bytes = Convert.FromBase64String(base64Data[(base64Data.IndexOf(',') + 1)..]);
            await File.WriteAllBytesAsync(Path.Combine(root, storageKey), bytes);
            return true;
        }

        public Task<BugScreenshotFile?> GetScreenshotAsync(string storageKey)
        {
            var path = Path.Combine(root, storageKey);
            if (!File.Exists(path))
            {
                return Task.FromResult<BugScreenshotFile?>(null);
            }

            var bytes = File.ReadAllBytes(path);
            return Task.FromResult<BugScreenshotFile?>(new BugScreenshotFile(bytes, "image/png"));
        }

        public Task<bool> DeleteScreenshotAsync(string storageKey)
        {
            DeleteCalls++;
            DeletedKey = storageKey;

            var path = Path.Combine(root, storageKey);
            if (!File.Exists(path))
            {
                return Task.FromResult(false);
            }

            File.Delete(path);
            return Task.FromResult(true);
        }

        public bool Exists(string storageKey) => File.Exists(Path.Combine(root, storageKey));
    }
}
