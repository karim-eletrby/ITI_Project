using Application.DTOs.SearchDtos;

namespace Application.DTOs.SearchDtos;

public record SearchResultDto(
    IReadOnlyList<UserSearchResultDto> Users
);

public record UserSearchResultDto(
    string Id,
    string DisplayName,
    string Username,
    string? Bio,
    string? ProfilePictureUrl,
    string FriendshipStatus = "None"
);
