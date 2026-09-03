using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.FriendshipDtos
{
    public record RespondFriendRequestDto(
    [Required] string RequesterId,
    [Required] FriendShipStatus Decision // FriendshipStatus.Accepted or FriendshipStatus.Rejected
);
}
