namespace Application.DTOs.PostsDtos
{
    public record SharedPostPreviewDto(
        int Id,
        string UserId,
        string AuthorName,
        string? AuthorProfilePictureUrl,
        string Content,
        string? MediaUrl,
        DateTime CreatedAt
    );
}
