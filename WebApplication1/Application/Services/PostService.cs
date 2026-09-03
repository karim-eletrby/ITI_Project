using Application.Common;
using Application.DTOs.MessageDtos;
using Application.DTOs.PostsDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using System.Text;

namespace Application.Services
{
    public class PostService : IPostService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationDispatcher _notificationDispatcher;
        private readonly IChatService _chatService;

        public PostService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            INotificationDispatcher notificationDispatcher,
            IChatService chatService)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _notificationDispatcher = notificationDispatcher;
            _chatService = chatService;
        }

        public async Task<Result<PostDto>> CreatePostAsync(string currentUserId, CreatePostDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.MediaUrl))
                throw new BadRequestException("Add some text or attach a photo/video.");

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
                throw new NotFoundException("User profile not found.");

            var post = new Post
            {
                UserId = currentUserId,
                Content = dto.Content?.Trim() ?? string.Empty,
                MediaUrl = dto.MediaUrl,
                Privacy = dto.Privacy,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Posts.AddAsync(post, ct);
            await _unitOfWork.CompleteAsync(ct);

            if (post.Privacy != PostPrivacy.OnlyMe)
            {
                var friendIds = await _unitOfWork.Friendships.GetAcceptedFriendIdsAsync(currentUserId, ct);
                foreach (var friendId in friendIds)
                {
                    await _notificationDispatcher.DispatchAsync(
                        friendId, currentUserId, NotificationType.PostInteraction,
                        $"{user.DisplayName} posted an update.",
                        $"/Feed#post-{post.Id}", ct);
                }
                await _unitOfWork.CompleteAsync(ct);
            }

            return Result<PostDto>.Success(MapToDto(post, user, currentUserId), "Post created successfully.");
        }

        public async Task<Result<PostDto>> GetPostByIdAsync(int postId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(post, currentUserId, ct);
            return Result<PostDto>.Success(MapToDto(post, post.User, currentUserId));
        }

        public async Task<Result<PagedResult<PostDto>>> GetFeedAsync(string currentUserId, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var friendIds = await _unitOfWork.Friendships.GetAcceptedFriendIdsAsync(currentUserId, ct);
            var pagedPosts = await _unitOfWork.Posts.GetFeedPostsAsync(currentUserId, friendIds, pageNumber, pageSize, ct);

            var items = pagedPosts.Items.Select(p => MapToDto(p, p.User, currentUserId)).ToList();
            return Result<PagedResult<PostDto>>.Success(new PagedResult<PostDto>(items, pagedPosts.TotalCount, pageNumber, pageSize));
        }

        public async Task<Result<PagedResult<PostDto>>> GetUserPostsAsync(
            string profileUserId, string currentUserId, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var viewerIsFriend = profileUserId == currentUserId;
            if (!viewerIsFriend)
            {
                var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(profileUserId, currentUserId, ct);
                viewerIsFriend = friendship?.Status == FriendShipStatus.Accepted;
            }

            var pagedPosts = await _unitOfWork.Posts.GetUserPostsAsync(profileUserId, currentUserId, viewerIsFriend, pageNumber, pageSize, ct);
            var items = pagedPosts.Items.Select(p => MapToDto(p, p.User, currentUserId)).ToList();
            return Result<PagedResult<PostDto>>.Success(new PagedResult<PostDto>(items, pagedPosts.TotalCount, pageNumber, pageSize));
        }

        public async Task<Result<bool>> DeletePostAsync(int postId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetByIdAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            if (post.UserId != currentUserId)
                throw new ForbiddenException("You cannot delete someone else's post.");

            await _unitOfWork.Posts.ClearShareReferencesAsync(postId, ct);
            _unitOfWork.Posts.Delete(post);
            await _unitOfWork.CompleteAsync(ct);
            return Result<bool>.Success(true, "Post deleted successfully.");
        }

        public async Task<Result<bool>> ToggleLikeAsync(int postId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            var existingLike = post.Likes.FirstOrDefault(l => l.UserId == currentUserId);
            var isLiked = false;

            if (existingLike != null)
            {
                post.Likes.Remove(existingLike);
            }
            else
            {
                post.Likes.Add(new PostLikes { PostId = postId, UserId = currentUserId, CreatedAt = DateTime.UtcNow });
                isLiked = true;

                var currentUser = await _userManager.FindByIdAsync(currentUserId);
                var actorName = currentUser?.DisplayName ?? "Someone";

                if (post.UserId != currentUserId)
                {
                    await _notificationDispatcher.DispatchAsync(
                        post.UserId, currentUserId, NotificationType.PostInteraction,
                        $"{actorName} liked your post.",
                        $"/Feed#post-{postId}", ct);
                }

                await NotifyOtherPostLikersAsync(
                    post,
                    currentUserId,
                    actorName,
                    $"{actorName} liked a post you liked.",
                    postId,
                    ct,
                    post.UserId);
            }

            await _unitOfWork.CompleteAsync(ct);
            return Result<bool>.Success(isLiked, isLiked ? "Post liked." : "Post unliked.");
        }

        public async Task<Result<CommentDto>> AddCommentAsync(int postId, string currentUserId, CreateCommentDto dto, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(post, currentUserId, ct);

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
                throw new NotFoundException("User not found.");

            Comment? parentComment = null;
            if (dto.ParentCommentId is int parentId)
            {
                parentComment = post.Comments.FirstOrDefault(c => c.Id == parentId);
                if (parentComment == null)
                    throw new BadRequestException("The comment you are replying to was not found.");
            }

            var comment = new Comment
            {
                PostId = postId,
                UserId = currentUserId,
                ParentCommentId = dto.ParentCommentId,
                Content = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Comment, int>().AddAsync(comment, ct);

            var skipLikerIds = new HashSet<string>(StringComparer.Ordinal) { currentUserId };

            if (parentComment != null && parentComment.UserId != currentUserId)
            {
                await _notificationDispatcher.DispatchAsync(
                    parentComment.UserId, currentUserId, NotificationType.PostInteraction,
                    $"{user.DisplayName} replied to your comment.",
                    $"/Feed#post-{postId}", ct);
                skipLikerIds.Add(parentComment.UserId);
            }

            if (parentComment == null && post.UserId != currentUserId)
            {
                await _notificationDispatcher.DispatchAsync(
                    post.UserId, currentUserId, NotificationType.PostInteraction,
                    $"{user.DisplayName} commented on your post.",
                    $"/Feed#post-{postId}", ct);
                skipLikerIds.Add(post.UserId);
            }
            else if (parentComment != null && post.UserId != currentUserId && !skipLikerIds.Contains(post.UserId))
            {
                await _notificationDispatcher.DispatchAsync(
                    post.UserId, currentUserId, NotificationType.PostInteraction,
                    $"{user.DisplayName} replied to a comment on your post.",
                    $"/Feed#post-{postId}", ct);
                skipLikerIds.Add(post.UserId);
            }

            var likerMessage = parentComment == null
                ? $"{user.DisplayName} commented on a post you liked."
                : $"{user.DisplayName} replied on a post you liked.";

            await NotifyOtherPostLikersAsync(
                post,
                currentUserId,
                user.DisplayName,
                likerMessage,
                postId,
                ct,
                skipLikerIds.ToArray());

            await _unitOfWork.CompleteAsync(ct);

            return Result<CommentDto>.Success(new CommentDto(
                comment.Id, postId, currentUserId, user.DisplayName, user.ProfilePictureUrl,
                comment.Content, comment.CreatedAt, true, comment.ParentCommentId, []),
                parentComment == null ? "Comment added successfully." : "Reply added successfully.");
        }

        public async Task<Result<IReadOnlyList<CommentDto>>> GetPostCommentsAsync(int postId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(post, currentUserId, ct);

            var canModeratePost = post.UserId == currentUserId;
            var comments = BuildCommentTree(post.Comments, canModeratePost, currentUserId);

            return Result<IReadOnlyList<CommentDto>>.Success(comments);
        }

        public async Task<Result<bool>> DeleteCommentAsync(int postId, int commentId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetByIdAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            var comment = await _unitOfWork.Repository<Comment, int>().GetByIdAsync(commentId, ct);
            if (comment == null || comment.PostId != postId)
                throw new NotFoundException("Comment not found.");

            if (comment.UserId != currentUserId && post.UserId != currentUserId)
                throw new ForbiddenException("You cannot delete this comment.");

            await DeleteCommentAndRepliesAsync(commentId, ct);
            await _unitOfWork.CompleteAsync(ct);
            return Result<bool>.Success(true, "Comment deleted successfully.");
        }

        private async Task DeleteCommentAndRepliesAsync(int commentId, CancellationToken ct)
        {
            var repo = _unitOfWork.Repository<Comment, int>();
            var replies = await repo.FindAsync(c => c.ParentCommentId == commentId, ct);

            foreach (var reply in replies)
                await DeleteCommentAndRepliesAsync(reply.Id, ct);

            var comment = await repo.GetByIdAsync(commentId, ct);
            if (comment != null)
                repo.Delete(comment);
        }

        public async Task<Result<IReadOnlyList<PostLikeUserDto>>> GetPostLikesAsync(int postId, string currentUserId, CancellationToken ct = default)
        {
            var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (post == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(post, currentUserId, ct);

            if (post.UserId != currentUserId)
                throw new ForbiddenException("Only the post owner can view who liked this post.");

            var likes = post.Likes
                .GroupBy(l => l.UserId)
                .Select(g => g.OrderByDescending(l => l.CreatedAt).First())
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new PostLikeUserDto(
                    l.UserId,
                    l.User.DisplayName,
                    l.User.ProfilePictureUrl,
                    l.CreatedAt))
                .ToList();

            return Result<IReadOnlyList<PostLikeUserDto>>.Success(likes);
        }

        public async Task<Result<PostDto>> SharePostToFeedAsync(int postId, string currentUserId, SharePostToFeedDto dto, CancellationToken ct = default)
        {
            var original = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (original == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(original, currentUserId, ct);

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
                throw new NotFoundException("User not found.");

            var sharePost = new Post
            {
                UserId = currentUserId,
                Content = dto.Caption?.Trim() ?? string.Empty,
                SharedPostId = original.Id,
                Privacy = dto.Privacy,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Posts.AddAsync(sharePost, ct);
            await _unitOfWork.CompleteAsync(ct);

            sharePost.User = user;
            sharePost.SharedPost = original;
            sharePost.SharedPost.User = original.User;

            if (original.UserId != currentUserId)
            {
                await _notificationDispatcher.DispatchAsync(
                    original.UserId, currentUserId, NotificationType.PostInteraction,
                    $"{user.DisplayName} shared your post.",
                    $"/Feed#post-{sharePost.Id}", ct);
            }

            if (sharePost.Privacy != PostPrivacy.OnlyMe)
            {
                var friendIds = await _unitOfWork.Friendships.GetAcceptedFriendIdsAsync(currentUserId, ct);
                foreach (var friendId in friendIds.Where(id => id != original.UserId))
                {
                    await _notificationDispatcher.DispatchAsync(
                        friendId, currentUserId, NotificationType.PostInteraction,
                        $"{user.DisplayName} shared a post.",
                        $"/Feed#post-{sharePost.Id}", ct);
                }
            }

            await _unitOfWork.CompleteAsync(ct);

            return Result<PostDto>.Success(MapToDto(sharePost, user, currentUserId), "Post shared to your profile.");
        }

        public async Task<Result<MessageDto>> SharePostToChatAsync(int postId, string currentUserId, SharePostToChatDto dto, CancellationToken ct = default)
        {
            var original = await _unitOfWork.Posts.GetPostWithDetailsAsync(postId, ct);
            if (original == null)
                throw new NotFoundException("Post not found.");

            await EnsureCanViewPostAsync(original, currentUserId, ct);

            var caption = dto.Message?.Trim() ?? string.Empty;
            var sendResult = await _chatService.SendMessageAsync(
                currentUserId,
                new SendMessageDto(dto.ReceiverId, caption, original.Id),
                ct);
            if (!sendResult.IsSuccess || sendResult.Data == null)
                throw new BadRequestException(sendResult.Message ?? "Could not send shared post.");

            return Result<MessageDto>.Success(sendResult.Data, "Post shared in chat.");
        }

        private async Task EnsureCanViewPostAsync(Post post, string currentUserId, CancellationToken ct)
        {
            if (post.Privacy == PostPrivacy.OnlyMe && post.UserId != currentUserId)
                throw new ForbiddenException("You do not have permission to view this post.");

            if (post.Privacy == PostPrivacy.FriendsOnly && post.UserId != currentUserId)
            {
                var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(post.UserId, currentUserId, ct);
                if (friendship == null || friendship.Status != FriendShipStatus.Accepted)
                    throw new ForbiddenException("This post is visible to friends only.");
            }
        }

        private static IReadOnlyList<CommentDto> BuildCommentTree(
            IEnumerable<Comment> comments,
            bool canModeratePost,
            string currentUserId)
        {
            var commentList = comments as IReadOnlyList<Comment> ?? comments.ToList();

            IReadOnlyList<CommentDto> MapReplies(int parentCommentId) =>
                commentList
                    .Where(c => c.ParentCommentId == parentCommentId)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => ToCommentDto(c, canModeratePost, currentUserId, MapReplies(c.Id)))
                    .ToList();

            return commentList
                .Where(c => c.ParentCommentId is null)
                .OrderBy(c => c.CreatedAt)
                .Select(c => ToCommentDto(c, canModeratePost, currentUserId, MapReplies(c.Id)))
                .ToList();
        }

        private static CommentDto ToCommentDto(
            Comment c,
            bool canModeratePost,
            string currentUserId,
            IReadOnlyList<CommentDto> replies) =>
            new(
                c.Id,
                c.PostId,
                c.UserId,
                c.User.DisplayName,
                c.User.ProfilePictureUrl,
                c.Content,
                c.CreatedAt,
                c.UserId == currentUserId || canModeratePost,
                c.ParentCommentId,
                replies);

        private async Task NotifyOtherPostLikersAsync(
            Post post,
            string actorId,
            string actorName,
            string message,
            int postId,
            CancellationToken ct,
            params string[] skipRecipientIds)
        {
            var skip = new HashSet<string>(skipRecipientIds, StringComparer.Ordinal)
            {
                actorId
            };

            foreach (var likerId in post.Likes.Select(l => l.UserId).Distinct())
            {
                if (skip.Contains(likerId))
                    continue;

                await _notificationDispatcher.DispatchAsync(
                    likerId,
                    actorId,
                    NotificationType.PostInteraction,
                    message,
                    $"/Feed#post-{postId}",
                    ct);

                skip.Add(likerId);
            }
        }

        private static int GetDistinctLikeCount(ICollection<PostLikes>? likes)
            => likes?.Select(l => l.UserId).Distinct().Count() ?? 0;

        private static bool IsLikedByUser(ICollection<PostLikes>? likes, string userId)
            => likes?.Any(l => l.UserId == userId) ?? false;

        private static PostDto MapToDto(Post post, ApplicationUser author, string currentUserId)
        {
            SharedPostPreviewDto? shared = null;
            if (post.SharedPost != null)
            {
                shared = new SharedPostPreviewDto(
                    post.SharedPost.Id,
                    post.SharedPost.UserId,
                    post.SharedPost.User?.DisplayName ?? "Unknown",
                    post.SharedPost.User?.ProfilePictureUrl,
                    post.SharedPost.Content,
                    post.SharedPost.MediaUrl,
                    post.SharedPost.CreatedAt);
            }

            return new PostDto(
                post.Id, post.UserId, author.DisplayName, author.ProfilePictureUrl,
                post.Content, post.MediaUrl, post.Privacy,
                GetDistinctLikeCount(post.Likes), post.Comments?.Count ?? 0,
                IsLikedByUser(post.Likes, currentUserId),
                post.CreatedAt, shared);
        }
    }
}
