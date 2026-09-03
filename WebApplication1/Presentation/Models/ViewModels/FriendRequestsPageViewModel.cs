using Application.DTOs.FriendshipDtos;
using Application.DTOs.SearchDtos;
using Domain.Enums;

namespace WebApplication1.Models.ViewModels;

public class FriendRequestsPageViewModel
{
    public IReadOnlyList<FriendshipResponseDto> IncomingRequests { get; init; } = Array.Empty<FriendshipResponseDto>();
    public IReadOnlyList<UserSearchResultDto> PeopleYouMayKnow { get; init; } = Array.Empty<UserSearchResultDto>();

    public int PendingRequestCount => IncomingRequests.Count(r => r.Status == FriendShipStatus.Pending);
}
