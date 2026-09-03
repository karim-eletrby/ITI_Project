namespace Application.DTOs.FriendshipDtos;

public record FriendBirthdayDto(
    string UserId,
    string DisplayName,
    string? ProfilePictureUrl
);
