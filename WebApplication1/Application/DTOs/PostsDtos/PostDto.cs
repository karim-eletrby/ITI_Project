using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.PostsDtos
{
    public record PostDto(
    int Id,
    string UserId,
    string AuthorName,
    string? AuthorProfilePictureUrl,
    string Content,
    string? MediaUrl,
    PostPrivacy Privacy,
    int LikesCount,
    int CommentsCount,
    bool IsLikedByCurrentUser,
    DateTime CreatedAt,
    SharedPostPreviewDto? SharedPost = null
);
}
