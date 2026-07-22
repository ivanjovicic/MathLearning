using MathLearning.Application.Services;
using MathLearning.Infrastructure.Services;
using MathLearning.Tests.Helpers;

namespace MathLearning.Tests.Services;

public sealed class BugReportServicePaginationTests
{
    [Fact]
    public async Task GetAll_IntMaxPaging_IsBoundedWithoutOverflow()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BugReportService(db, new NoOpScreenshotStorageService());

        var result = await service.GetBugReportsAsync(int.MaxValue, int.MaxValue);

        Assert.Empty(result.Bugs);
        Assert.Equal(1_000, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetMine_IntMaxPaging_IsBoundedWithoutOverflow()
    {
        await using var db = TestDbContextFactory.Create();
        var service = new BugReportService(db, new NoOpScreenshotStorageService());

        var result = await service.GetMyBugReportsAsync("user-1", int.MaxValue, int.MaxValue);

        Assert.Empty(result.Bugs);
        Assert.Equal(1_000, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    private sealed class NoOpScreenshotStorageService : IScreenshotStorageService
    {
        public Task<bool> UploadScreenshotAsync(string base64Data, string storageKey) =>
            Task.FromResult(false);

        public Task<BugScreenshotFile?> GetScreenshotAsync(string storageKey) =>
            Task.FromResult<BugScreenshotFile?>(null);

        public Task<bool> DeleteScreenshotAsync(string storageKey) => Task.FromResult(true);
    }
}
