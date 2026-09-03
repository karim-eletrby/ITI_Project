using Application.Common;
using Application.DTOs.MessageDtos;
using Application.DTOs.PostsDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IPostService
    {
        Task<Result<PostDto>> CreatePostAsync(string currentUserId, CreatePostDto dto, CancellationToken ct = default);
        Task<Result<PostDto>> GetPostByIdAsync(int postId, string currentUserId, CancellationToken ct = default);
        Task<Result<PagedResult<PostDto>>> GetFeedAsync(string currentUserId, int pageNumber, int pageSize, CancellationToken ct = default);
        Task<Result<PagedResult<PostDto>>> GetUserPostsAsync(string profileUserId, string currentUserId, int pageNumber, int pageSize, CancellationToken ct = default);
        Task<Result<bool>> DeletePostAsync(int postId, string currentUserId, CancellationToken ct = default);
        Task<Result<bool>> ToggleLikeAsync(int postId, string currentUserId, CancellationToken ct = default);
        Task<Result<CommentDto>> AddCommentAsync(int postId, string currentUserId, CreateCommentDto dto, CancellationToken ct = default);
        Task<Result<IReadOnlyList<CommentDto>>> GetPostCommentsAsync(int postId, string currentUserId, CancellationToken ct = default);
        Task<Result<bool>> DeleteCommentAsync(int postId, int commentId, string currentUserId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<PostLikeUserDto>>> GetPostLikesAsync(int postId, string currentUserId, CancellationToken ct = default);
        Task<Result<PostDto>> SharePostToFeedAsync(int postId, string currentUserId, SharePostToFeedDto dto, CancellationToken ct = default);
        Task<Result<MessageDto>> SharePostToChatAsync(int postId, string currentUserId, SharePostToChatDto dto, CancellationToken ct = default);
    }
}
