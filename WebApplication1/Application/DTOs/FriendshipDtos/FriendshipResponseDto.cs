using Domain.Enums;

namespace Application.DTOs.FriendshipDtos
{
    public record FriendshipResponseDto(
      string RequesterId,
      string RequesterName,
      string? RequesterProfilePictureUrl,
      string ReceiverId,
      string ReceiverName,
      string? ReceiverProfilePictureUrl,
      FriendShipStatus Status,
      DateTime CreatedAt,
      DateTime? UpdatedAt
  );
}
