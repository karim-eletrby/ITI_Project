namespace Application.DTOs.PostsDtos
{
    public record PostLikeUserDto(
        string UserId,
        string DisplayName,
        string? ProfilePictureUrl,
        DateTime LikedAt
    );
}
