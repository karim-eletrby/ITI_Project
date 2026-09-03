using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.PostsDtos
{
    public record PostLikeDto(
    int PostId,
    string UserId,
    string UserName,
    string? UserProfilePictureUrl,
    DateTime LikedAt
);
}
