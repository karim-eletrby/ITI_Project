using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.PostsDtos
{
    public record CommentDto(
    int Id,
    int PostId,
    string UserId,
    string AuthorName,
    string? AuthorProfilePictureUrl,
    string Content,
    DateTime CreatedAt,
    bool CanDelete = false,
    int? ParentCommentId = null,
    IReadOnlyList<CommentDto>? Replies = null
);
}
