namespace Application.DTOs.Auth
{
    public record UserProfileDto(
        string Id,
        string DisplayName,
        string Username,
        string Email,
        bool EmailConfirmed,
        string? Bio,
        string? ProfilePictureUrl,
        string? CoverPictureUrl,
        DateOnly DateOfBirth,
        int FriendsCount,
        int PostsCount,
        DateTime CreatedAt
    );
}
