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

    public async Task<string?> UploadScreenshotAsync(string base64Data, string fileName)
    {
        try
        {
            // Validate base64 format
            if (!base64Data.Contains(","))
            {
                throw new ArgumentException("Invalid base64 data format");
            }

            var parts = base64Data.Split(',');
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid base64 data format");
            }

            var base64 = parts[1];
            var bytes = Convert.FromBase64String(base64);

            // Check file size
            if (bytes.Length > MaxFileSizeBytes)
            {
                throw new ArgumentException($"Screenshot too large. Maximum size is {MaxFileSizeBytes / 1024 / 1024}MB");
            }

            // Validate image format (basic check)
            if (!IsValidImage(bytes))
            {
                throw new ArgumentException("Invalid image format");
            }

            var filePath = Path.Combine(_uploadsPath, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);

            // Return relative URL
            return $"/uploads/screenshots/{fileName}";
        }
        catch (Exception ex)
        {
            // Log error and return null
            Console.WriteLine($"Screenshot upload failed: {ex.Message}");
            return null;
        }
    }

    public Task<bool> DeleteScreenshotAsync(string url)
    {
        try
        {
            var fileName = Path.GetFileName(url);
            var filePath = Path.Combine(_uploadsPath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private bool IsValidImage(byte[] bytes)
    {
        // Check PNG signature
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        // Check JPEG signature
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return true;
        }

        return false;
    }
}
