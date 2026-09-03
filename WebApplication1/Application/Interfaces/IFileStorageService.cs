namespace Application.Interfaces;

public enum StoredMediaKind
{
    ProfileImage,
    CoverImage,
    PostMedia
}

public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        StoredMediaKind kind,
        string? contentType = null,
        CancellationToken ct = default);
    void DeleteByUrl(string? relativeUrl);
}
