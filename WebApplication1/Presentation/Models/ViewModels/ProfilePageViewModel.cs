using Application.DTOs.Auth;
using Application.DTOs.FriendshipDtos;
using Application.DTOs.PostsDtos;
using Application.Common;

namespace WebApplication1.Models.ViewModels;

public class ProfilePageViewModel
{
    public const int FriendsDisplayLimit = 4;

    public required UserProfileDto Profile { get; init; }
    public IReadOnlyList<FriendSummaryDto> Friends { get; init; } = Array.Empty<FriendSummaryDto>();
    public PagedResult<PostDto> Posts { get; init; } = new([], 0, 1, 10);
    public bool IsCurrentUser { get; init; }
    public string FriendshipStatus { get; init; } = "None";

    public bool HasMoreFriends => Friends.Count > FriendsDisplayLimit;
}
