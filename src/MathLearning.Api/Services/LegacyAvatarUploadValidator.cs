namespace MathLearning.Api.Services;

public static class LegacyAvatarUploadValidator
{
    public const long MaxFileBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
    };

    public sealed record ValidationResult(bool Success, string? Error, string NormalizedExtension);

    public static async Task<ValidationResult> ValidateAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            return new(false, "No file uploaded.", string.Empty);

        if (file.Length > MaxFileBytes)
            return new(false, "File exceeds maximum allowed size.", string.Empty);

        var declaredExtension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrEmpty(declaredExtension) && !AllowedExtensions.Contains(declaredExtension))
            return new(false, "Unsupported file type.", string.Empty);

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, 12), ct);
        if (read < 3)
            return new(false, "Invalid image content.", string.Empty);

        if (!TryResolveImageExtension(header.AsSpan(0, read), out var detectedExtension))
            return new(false, "Invalid image content.", string.Empty);

        if (!string.IsNullOrEmpty(declaredExtension) &&
            !ExtensionsMatch(declaredExtension, detectedExtension))
        {
            return new(false, "File content does not match declared type.", string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !ContentTypeMatchesExtension(file.ContentType, detectedExtension))
        {
            return new(false, "File content does not match declared type.", string.Empty);
        }

        return new(true, null, detectedExtension);
    }

    public static bool IsSafeFileName(string userId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
            return false;

        if (!safeName.StartsWith($"{userId}_", StringComparison.Ordinal))
            return false;

        return AllowedExtensions.Contains(Path.GetExtension(safeName));
    }

    public static string BuildStorageFileName(string userId, string normalizedExtension) =>
        $"{userId}_{Guid.NewGuid():N}{normalizedExtension}";

    private static bool ExtensionsMatch(string declaredExtension, string detectedExtension) =>
        NormalizeExtension(declaredExtension).Equals(
            NormalizeExtension(detectedExtension),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExtension(string extension) =>
        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : extension.ToLowerInvariant();

    private static bool ContentTypeMatchesExtension(string contentType, string extension)
    {
        var expected = NormalizeExtension(extension) switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null,
        };

        return expected == null ||
               contentType.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveImageExtension(ReadOnlySpan<byte> header, out string extension)
    {
        extension = string.Empty;

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            extension = ".png";
            return true;
        }

        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            extension = ".webp";
            return true;
        }

        return false;
    }
}
