using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.FriendshipDtos
{
    public record FriendSummaryDto(
    string UserId,
    string DisplayName,
    string Username,
    string? ProfilePictureUrl,
    string? Bio,
    DateTime FriendsSince
);
}
