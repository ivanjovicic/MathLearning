using MathLearning.Application.DTOs.Bugs;
using MathLearning.Application.Services;

namespace MathLearning.Infrastructure.Services;

public class LocalScreenshotStorageService : IScreenshotStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private readonly string _uploadsPath;

    public LocalScreenshotStorageService()
    {
        _uploadsPath = Path.Combine(AppContext.BaseDirectory, "uploads", "screenshots");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<bool> UploadScreenshotAsync(string base64Data, string storageKey)
    {
        try
        {
            if (!IsSafeStorageKey(storageKey))
            {
                return false;
            }

            if (!TryDecodeImage(base64Data, out var bytes))
            {
                return false;
            }

            var filePath = Path.Combine(_uploadsPath, storageKey);
            await File.WriteAllBytesAsync(filePath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<BugScreenshotFile?> GetScreenshotAsync(string storageKey)
    {
        if (!IsSafeStorageKey(storageKey))
        {
            return null;
        }

        var filePath = Path.Combine(_uploadsPath, storageKey);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var contentType = TryGetContentType(bytes);
            return contentType is null ? null : new BugScreenshotFile(bytes, contentType);
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> DeleteScreenshotAsync(string storageKey)
    {
        try
        {
            if (!IsSafeStorageKey(storageKey))
            {
                return Task.FromResult(false);
            }

            var filePath = Path.Combine(_uploadsPath, storageKey);
            if (!File.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            File.Delete(filePath);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static bool TryDecodeImage(string base64Data, out byte[] bytes)
    {
        bytes = [];

        if (string.IsNullOrWhiteSpace(base64Data))
        {
            return false;
        }

        var commaIndex = base64Data.IndexOf(',');
        if (commaIndex < 0 || commaIndex == base64Data.Length - 1)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(base64Data[(commaIndex + 1)..]);
        }
        catch
        {
            return false;
        }

        if (bytes.Length > MaxFileSizeBytes)
        {
            return false;
        }

        return TryGetContentType(bytes) is not null;
    }

    private static string? TryGetContentType(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return "image/jpeg";
        }

        return null;
    }

    private static bool IsSafeStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return false;
        }

        if (!string.Equals(storageKey, Path.GetFileName(storageKey), StringComparison.Ordinal))
        {
            return false;
        }

        return storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}
