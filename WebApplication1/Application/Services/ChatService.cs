using Application.Common;
using Application.DTOs.MessageDtos;
using Application.DTOs.PostsDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationDispatcher _notificationDispatcher;
        private readonly IRealtimeChatService _realtimeChat;

        public ChatService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            INotificationDispatcher notificationDispatcher,
            IRealtimeChatService realtimeChat)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _notificationDispatcher = notificationDispatcher;
            _realtimeChat = realtimeChat;
        }

        public async Task<Result<MessageDto>> SendMessageAsync(string senderId, SendMessageDto dto, CancellationToken ct = default)
        {
            if (senderId == dto.ReceiverId)
                throw new BadRequestException("You cannot send a message to yourself.");

            var receiver = await _userManager.FindByIdAsync(dto.ReceiverId);
            if (receiver == null)
                throw new NotFoundException("Recipient user not found.");

            var sender = await _userManager.FindByIdAsync(senderId);
            if (sender == null)
                throw new NotFoundException("Sender user not found.");

            if (string.IsNullOrWhiteSpace(dto.Content) && !dto.SharedPostId.HasValue)
                throw new BadRequestException("Message cannot be empty.");

            Post? sharedPost = null;
            if (dto.SharedPostId.HasValue)
            {
                sharedPost = await _unitOfWork.Posts.GetPostWithDetailsAsync(dto.SharedPostId.Value, ct);
                if (sharedPost == null)
                    throw new NotFoundException("Shared post not found.");
                await EnsureCanViewPostAsync(sharedPost, senderId, ct);
            }

            var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(senderId, dto.ReceiverId, ct);
            if (friendship?.Status == FriendShipStatus.Blocked)
                throw new ForbiddenException("You cannot message this user.");

            var isFriend = friendship != null && friendship.Status == FriendShipStatus.Accepted;
            var messageRepo = _unitOfWork.Repository<Message, int>();

            var conversationMessages = await messageRepo.FindAsync(
                m => (m.SenderId == senderId && m.ReceiverId == dto.ReceiverId) ||
                     (m.SenderId == dto.ReceiverId && m.ReceiverId == senderId), ct);

            var isAccepted = isFriend || IsConversationAccepted(conversationMessages, senderId, dto.ReceiverId);

            if (!isFriend && !isAccepted)
            {
                var priorSent = conversationMessages.Count(m => m.SenderId == senderId);
                if (priorSent > 0)
                {
                    throw new BadRequestException(
                        "You can only send one message until they accept your message by replying.");
                }
            }

            var otherHasSent = conversationMessages.Any(m => m.SenderId == dto.ReceiverId);
            var senderPriorSent = conversationMessages.Count(m => m.SenderId == senderId);
            var isAcceptingReply = !isFriend && !isAccepted && senderPriorSent == 0 && otherHasSent;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = dto.ReceiverId,
                Content = dto.Content?.Trim() ?? string.Empty,
                SharedPostId = dto.SharedPostId,
                IsRequest = !isFriend && !isAccepted && !isAcceptingReply,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await messageRepo.AddAsync(message, ct);

            if (message.IsRequest)
            {
                await _notificationDispatcher.DispatchAsync(
                    dto.ReceiverId,
                    senderId,
                    NotificationType.MessageRequest,
                    $"{sender.DisplayName} sent you a message request.",
                    $"/Chat?id={senderId}",
                    ct);
            }
            else
            {
                var notificationText = dto.SharedPostId.HasValue
                    ? $"{sender.DisplayName} shared a post with you."
                    : BuildMessageNotificationText(sender.DisplayName, message.Content);

                await _notificationDispatcher.DispatchAsync(
                    dto.ReceiverId,
                    senderId,
                    NotificationType.NewMessage,
                    notificationText,
                    $"/Chat?id={senderId}",
                    ct);
            }

            await _unitOfWork.CompleteAsync(ct);

            if (sharedPost != null)
                message.SharedPost = sharedPost;

            var messageDto = MapToDto(message, sender, receiver, sharedPost);

            await _realtimeChat.PushMessageToUserAsync(dto.ReceiverId, messageDto, ct);
            await _realtimeChat.PushMessageToUserAsync(senderId, messageDto, ct);

            return Result<MessageDto>.Success(messageDto, "Message sent successfully.");
        }

        public async Task<Result<IReadOnlyList<MessageDto>>> GetConversationAsync(string currentUserId, string otherUserId, CancellationToken ct = default)
        {
            var messageRepo = _unitOfWork.Repository<Message, int>();
            var messages = await messageRepo.FindAsync(m =>
                (m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                (m.SenderId == otherUserId && m.ReceiverId == currentUserId), ct);

            var otherUser = await _userManager.FindByIdAsync(otherUserId);
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (otherUser == null || currentUser == null)
                throw new NotFoundException("User not found.");

            var sharedPosts = await LoadSharedPostsAsync(messages, ct);

            var result = messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => MapToDto(
                    m,
                    m.SenderId == currentUserId ? currentUser : otherUser,
                    m.SenderId == currentUserId ? otherUser : currentUser,
                    m.SharedPostId.HasValue && sharedPosts.TryGetValue(m.SharedPostId.Value, out var post) ? post : null))
                .ToList();

            return Result<IReadOnlyList<MessageDto>>.Success(result);
        }

        public async Task<Result<IReadOnlyList<ConversationSummaryDto>>> GetConversationsSummaryAsync(string currentUserId, CancellationToken ct = default)
        {
            var messageRepo = _unitOfWork.Repository<Message, int>();
            var allUserMessages = await messageRepo.FindAsync(m =>
                m.SenderId == currentUserId || m.ReceiverId == currentUserId, ct);

            var groupedConversations = allUserMessages
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .ToList();

            var summaries = new List<ConversationSummaryDto>();

            foreach (var group in groupedConversations)
            {
                var otherUserId = group.Key;
                var otherUser = await _userManager.FindByIdAsync(otherUserId);
                if (otherUser == null) continue;

                var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(currentUserId, otherUserId, ct);
                var isFriend = friendship?.Status == FriendShipStatus.Accepted;
                var isAccepted = isFriend || IsConversationAccepted(group, currentUserId, otherUserId);

                var lastMessage = group.OrderByDescending(m => m.CreatedAt).First();
                var unreadCount = group.Count(m => m.ReceiverId == currentUserId && !m.IsRead);

                summaries.Add(new ConversationSummaryDto(
                    otherUserId,
                    otherUser.DisplayName,
                    otherUser.ProfilePictureUrl,
                    lastMessage.SharedPostId.HasValue ? "📎 Shared a post" : lastMessage.Content,
                    lastMessage.CreatedAt,
                    unreadCount,
                    !isAccepted
                ));
            }

            return Result<IReadOnlyList<ConversationSummaryDto>>.Success(
                summaries.OrderByDescending(s => s.LastMessageSentAt).ToList()
            );
        }

        public async Task<Result<ConversationContextDto>> GetConversationContextAsync(
            string currentUserId,
            string otherUserId,
            CancellationToken ct = default)
        {
            if (currentUserId == otherUserId)
                throw new BadRequestException("You cannot start a conversation with yourself.");

            var otherUser = await _userManager.FindByIdAsync(otherUserId);
            if (otherUser == null)
                throw new NotFoundException("User not found.");

            var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(currentUserId, otherUserId, ct);
            var relationship = ResolveRelationshipStatus(friendship, currentUserId);
            var isFriend = friendship?.Status == FriendShipStatus.Accepted;

            var messageRepo = _unitOfWork.Repository<Message, int>();
            var conversationMessages = await messageRepo.FindAsync(
                m => (m.SenderId == currentUserId && m.ReceiverId == otherUserId) ||
                     (m.SenderId == otherUserId && m.ReceiverId == currentUserId), ct);

            var isAccepted = isFriend || IsConversationAccepted(conversationMessages, currentUserId, otherUserId);
            var isRequestConversation = !isAccepted;

            string? blockedReason = null;
            var canSend = true;

            if (friendship?.Status == FriendShipStatus.Blocked)
            {
                canSend = false;
                blockedReason = "You cannot message this user.";
            }
            else if (!isFriend && !isAccepted)
            {
                var sentCount = conversationMessages.Count(m => m.SenderId == currentUserId);
                if (sentCount >= 1)
                {
                    canSend = false;
                    blockedReason = "You already sent your one message. Wait until they accept by replying.";
                }
            }

            var currentUserSent = conversationMessages.Count(m => m.SenderId == currentUserId);
            var otherHasSent = conversationMessages.Any(m => m.SenderId == otherUserId);
            var isIncomingRequest = isRequestConversation && otherHasSent && currentUserSent == 0;
            var isOutgoingRequest = isRequestConversation && currentUserSent > 0;

            var context = new ConversationContextDto(
                otherUserId,
                otherUser.DisplayName,
                otherUser.ProfilePictureUrl,
                isFriend,
                canSend,
                blockedReason,
                isRequestConversation,
                relationship,
                isIncomingRequest,
                isOutgoingRequest);

            return Result<ConversationContextDto>.Success(context);
        }

        private static string BuildMessageNotificationText(string senderName, string content)
        {
            var trimmed = content?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
                return $"{senderName} sent you a message.";

            const int maxPreview = 80;
            var preview = trimmed.Length <= maxPreview ? trimmed : $"{trimmed[..maxPreview]}…";
            return $"{senderName}: {preview}";
        }

        private static bool IsConversationAccepted(IEnumerable<Message> messages, string userA, string userB)
        {
            var aToB = messages.Any(m => m.SenderId == userA && m.ReceiverId == userB);
            var bToA = messages.Any(m => m.SenderId == userB && m.ReceiverId == userA);
            return aToB && bToA;
        }

        private static string ResolveRelationshipStatus(Friendship? friendship, string currentUserId)
        {
            if (friendship is null)
                return "None";

            return friendship.Status switch
            {
                FriendShipStatus.Accepted => "Friends",
                FriendShipStatus.Blocked => "Blocked",
                FriendShipStatus.Pending when friendship.RequesterId == currentUserId => "PendingSent",
                FriendShipStatus.Pending => "PendingReceived",
                _ => "None"
            };
        }

        public async Task<Result<bool>> MarkAsReadAsync(string currentUserId, string senderId, CancellationToken ct = default)
        {
            var messageRepo = _unitOfWork.Repository<Message, int>();
            var unreadMessages = await messageRepo.FindAsync(m =>
                m.SenderId == senderId && m.ReceiverId == currentUserId && !m.IsRead, ct);

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
                messageRepo.Update(msg);
            }

            await _unitOfWork.CompleteAsync(ct);
            return Result<bool>.Success(true, "Messages marked as read.");
        }

        private static MessageDto MapToDto(Message message, ApplicationUser sender, ApplicationUser receiver, Post? sharedPost = null)
        {
            SharedPostPreviewDto? preview = null;
            if (sharedPost != null)
            {
                preview = new SharedPostPreviewDto(
                    sharedPost.Id,
                    sharedPost.UserId,
                    sharedPost.User?.DisplayName ?? "Unknown",
                    sharedPost.User?.ProfilePictureUrl,
                    sharedPost.Content,
                    sharedPost.MediaUrl,
                    sharedPost.CreatedAt);
            }

            return new MessageDto(
                message.Id,
                sender.Id,
                sender.DisplayName,
                sender.ProfilePictureUrl,
                receiver.Id,
                receiver.DisplayName,
                receiver.ProfilePictureUrl,
                message.Content,
                message.IsRequest,
                message.IsRead,
                message.ReadAt,
                message.CreatedAt,
                preview);
        }

        private async Task<Dictionary<int, Post>> LoadSharedPostsAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            var ids = messages
                .Where(m => m.SharedPostId.HasValue)
                .Select(m => m.SharedPostId!.Value)
                .Distinct()
                .ToList();

            var map = new Dictionary<int, Post>();
            foreach (var id in ids)
            {
                var post = await _unitOfWork.Posts.GetPostWithDetailsAsync(id, ct);
                if (post != null)
                    map[id] = post;
            }

            return map;
        }

        private async Task EnsureCanViewPostAsync(Post post, string currentUserId, CancellationToken ct)
        {
            if (post.Privacy == PostPrivacy.OnlyMe && post.UserId != currentUserId)
                throw new ForbiddenException("You do not have permission to share this post.");

            if (post.Privacy == PostPrivacy.FriendsOnly && post.UserId != currentUserId)
            {
                var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(post.UserId, currentUserId, ct);
                if (friendship == null || friendship.Status != FriendShipStatus.Accepted)
                    throw new ForbiddenException("This post is visible to friends only.");
            }
        }
    }
}
