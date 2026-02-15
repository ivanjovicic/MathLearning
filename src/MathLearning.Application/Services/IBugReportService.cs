using MathLearning.Application.DTOs.Bugs;

namespace MathLearning.Application.Services;

public interface IBugReportService
{
    Task<BugReportDto> CreateBugReportAsync(string userId, BugReportRequest request);
    Task<BugReportsResponse> GetBugReportsAsync(int page = 1, int pageSize = 20, string? status = null, string? severity = null);
    Task<BugReportsResponse> GetMyBugReportsAsync(string userId, int page = 1, int pageSize = 50);
    Task<BugReportDto?> GetBugReportAsync(Guid id);
    Task<bool> UpdateBugStatusAsync(Guid id, UpdateBugStatusRequest request);
}

public interface IScreenshotStorageService
{
    Task<string?> UploadScreenshotAsync(string base64Data, string fileName);
    Task<bool> DeleteScreenshotAsync(string url);
}
