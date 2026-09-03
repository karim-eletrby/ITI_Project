using Application.DTOs.Auth;
using Application.DTOs.FriendshipDtos;

namespace WebApplication1.Models.ViewModels;

public class ProfileFriendsPageViewModel
{
    public required UserProfileDto Profile { get; init; }
    public IReadOnlyList<FriendSummaryDto> Friends { get; init; } = Array.Empty<FriendSummaryDto>();
    public bool IsCurrentUser { get; init; }
}
