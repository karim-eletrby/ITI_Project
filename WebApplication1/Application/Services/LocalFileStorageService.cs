using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace Application.Services;

public class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".jfif", ".bmp" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".webm", ".mov", ".m4v", ".3gp" };

    private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/bmp"] = ".bmp",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov",
        ["video/x-m4v"] = ".m4v",
        ["video/3gpp"] = ".3gp",
    };

    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        StoredMediaKind kind,
        string? contentType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
            throw new InvalidOperationException("Uploads are unavailable because wwwroot is not configured.");

        var (subfolder, maxBytes, allowVideo) = kind switch
        {
            StoredMediaKind.ProfileImage => ("profiles", 5 * 1024 * 1024, false),
            StoredMediaKind.CoverImage => ("covers", 8 * 1024 * 1024, false),
            StoredMediaKind.PostMedia => ("posts", 1000 * 1024 * 1024, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var extension = TryResolveExtension(originalFileName, contentType, allowVideo);

        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsRoot);

        var storedName = $"{Guid.NewGuid():N}{extension ?? ".bin"}";
        var physicalPath = Path.Combine(uploadsRoot, storedName);

        await using var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None);

        long totalBytes;
        string? sniffedExtension = null;

        if (content.CanSeek)
        {
            if (content.Length > maxBytes)
                throw new InvalidOperationException($"File is too large. Maximum size is {maxBytes / (1024 * 1024)} MB.");

            if (extension is null)
            {
                sniffedExtension = await SniffExtensionAsync(content, allowVideo, ct);
                extension = sniffedExtension;
            }

            await content.CopyToAsync(fileStream, ct);
            totalBytes = fileStream.Length;
        }
        else
        {
            var copyResult = await CopyWithLimitAsync(content, fileStream, maxBytes, allowVideo, ct);
            totalBytes = copyResult.TotalBytes;
            sniffedExtension = copyResult.SniffedExtension;
            extension ??= sniffedExtension;
        }

        if (totalBytes == 0)
        {
            fileStream.Close();
            File.Delete(physicalPath);
            throw new InvalidOperationException("The uploaded file is empty.");
        }

        if (totalBytes > maxBytes)
        {
            fileStream.Close();
            File.Delete(physicalPath);
            throw new InvalidOperationException($"File is too large. Maximum size is {maxBytes / (1024 * 1024)} MB.");
        }

        extension ??= sniffedExtension;
        if (extension is null || !IsAllowedExtension(extension, allowVideo))
        {
            fileStream.Close();
            File.Delete(physicalPath);
            throw new InvalidOperationException(allowVideo
                ? "Allowed formats: JPG, PNG, GIF, WEBP, MP4, WEBM, MOV."
                : "Allowed formats: JPG, PNG, GIF, WEBP.");
        }

        if (!storedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            var correctedPath = Path.Combine(uploadsRoot, $"{Guid.NewGuid():N}{extension}");
            fileStream.Close();
            File.Move(physicalPath, correctedPath, overwrite: true);
            storedName = Path.GetFileName(correctedPath);
        }

        return $"/uploads/{subfolder}/{storedName}";
    }

    public void DeleteByUrl(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
            return;

        var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads"));

        if (!physicalPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);
    }

    private static bool IsAllowedExtension(string extension, bool allowVideo)
    {
        var isImage = ImageExtensions.Contains(extension);
        var isVideo = allowVideo && VideoExtensions.Contains(extension);
        return isImage || isVideo;
    }

    private static string? TryResolveExtension(string originalFileName, string? contentType, bool allowVideo)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            extension = extension.ToLowerInvariant();
            return IsAllowedExtension(extension, allowVideo) ? extension : null;
        }

        var normalizedType = contentType?.Split(';')[0].Trim();
        if (!string.IsNullOrWhiteSpace(normalizedType)
            && ContentTypeExtensions.TryGetValue(normalizedType, out var mapped)
            && IsAllowedExtension(mapped, allowVideo))
        {
            return mapped;
        }

        if (allowVideo && !string.IsNullOrWhiteSpace(normalizedType)
            && normalizedType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedType switch
            {
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                "video/x-m4v" => ".m4v",
                "video/3gpp" => ".3gp",
                _ => ".mp4"
            };
        }

        return null;
    }

    private static async Task<string?> SniffExtensionAsync(Stream content, bool allowVideo, CancellationToken ct)
    {
        if (!content.CanSeek)
            return null;

        var position = content.Position;
        var header = new byte[16];
        var read = await content.ReadAsync(header.AsMemory(0, header.Length), ct);
        content.Position = position;
        return DetectExtensionFromHeader(header.AsSpan(0, read), allowVideo);
    }

    private static string? DetectExtensionFromHeader(ReadOnlySpan<byte> header, bool allowVideo)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return ".png";

        if (header.Length >= 3 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
            return ".gif";

        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ".webp";

        if (!allowVideo)
            return null;

        if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            return ".webm";

        if (header.Length >= 8
            && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
        {
            if (header.Length >= 12)
            {
                var brand = System.Text.Encoding.ASCII.GetString(header.Slice(8, 4));
                if (brand is "qt  " or "moov")
                    return ".mov";
            }

            return ".mp4";
        }

        return null;
    }

    private sealed record CopyResult(long TotalBytes, string? SniffedExtension);

    private static async Task<CopyResult> CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maxBytes,
        bool allowVideo,
        CancellationToken ct)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;
        string? sniffedExtension = null;
        int read;

        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            if (sniffedExtension is null)
                sniffedExtension = DetectExtensionFromHeader(buffer.AsSpan(0, read), allowVideo);

            totalBytes += read;
            if (totalBytes > maxBytes)
                throw new InvalidOperationException($"File is too large. Maximum size is {maxBytes / (1024 * 1024)} MB.");

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return new CopyResult(totalBytes, sniffedExtension);
    }
}
