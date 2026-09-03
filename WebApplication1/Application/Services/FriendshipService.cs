using Application.Common;
using Application.DTOs.FriendshipDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Application.Services
{
    public class FriendshipService : IFriendshipService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationDispatcher _notificationDispatcher;

        public FriendshipService(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            INotificationDispatcher notificationDispatcher)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _notificationDispatcher = notificationDispatcher;
        }

        public async Task<Result<FriendshipResponseDto>> SendRequestAsync(string requesterId, SendFriendRequestDto dto, CancellationToken ct = default)
        {
            if (requesterId == dto.ReceiverId)
                throw new BadRequestException("You cannot send a friend request to yourself.");

            var receiver = await _userManager.FindByIdAsync(dto.ReceiverId);
            if (receiver == null)
                throw new NotFoundException("User to add was not found.");

            var requester = await _userManager.FindByIdAsync(requesterId);
            if (requester == null)
                throw new NotFoundException("Requester profile was not found.");

            var existingFriendship = await _unitOfWork.Friendships.GetFriendshipAsync(requesterId, dto.ReceiverId, ct);
            if (existingFriendship != null)
            {
                if (existingFriendship.Status == FriendShipStatus.Accepted)
                    throw new ConflictException("You are already friends with this user.");

                if (existingFriendship.Status == FriendShipStatus.Pending)
                    throw new ConflictException("A friend request is already pending between you.");

                if (existingFriendship.Status == FriendShipStatus.Blocked)
                    throw new ForbiddenException("Unable to send request to this user.");

                // If previously rejected, allow re-requesting by updating status back to Pending
                existingFriendship.RequesterId = requesterId;
                existingFriendship.ReceiverId = dto.ReceiverId;
                existingFriendship.Status = FriendShipStatus.Pending;
                existingFriendship.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Friendships.Update(existingFriendship);
            }
            else
            {
                var friendship = new Friendship
                {
                    RequesterId = requesterId,
                    ReceiverId = dto.ReceiverId,
                    Status = FriendShipStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Friendships.AddAsync(friendship, ct);
            }

            // Notify receiver in-app and via SignalR
            await _notificationDispatcher.DispatchAsync(
                dto.ReceiverId,
                requesterId,
                NotificationType.FriendRequest,
                $"{requester.DisplayName} sent you a friend request.",
                "/Friendships/Pending",
                ct);

            await _unitOfWork.CompleteAsync(ct);

            var responseDto = new FriendshipResponseDto(
                requesterId,
                requester.DisplayName,
                requester.ProfilePictureUrl,
                receiver.Id,
                receiver.DisplayName,
                receiver.ProfilePictureUrl,
                FriendShipStatus.Pending,
                DateTime.UtcNow,
                null
            );

            return Result<FriendshipResponseDto>.Success(responseDto, "Friend request sent successfully.");
        }

        public async Task<Result<FriendshipResponseDto>> RespondToRequestAsync(string currentUserId, RespondFriendRequestDto dto, CancellationToken ct = default)
        {
            var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(dto.RequesterId, currentUserId, ct);
            if (friendship == null || friendship.ReceiverId != currentUserId)
                throw new NotFoundException("Friend request not found.");

            if (friendship.Status != FriendShipStatus.Pending)
                throw new BadRequestException($"Cannot respond to a friend request with status '{friendship.Status}'.");

            if (dto.Decision != FriendShipStatus.Accepted && dto.Decision != FriendShipStatus.Rejected && dto.Decision != FriendShipStatus.Blocked)
                throw new BadRequestException("Invalid decision status provided.");

            friendship.Status = dto.Decision;
            friendship.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Friendships.Update(friendship);

            // Notify the original requester if accepted
            if (dto.Decision == FriendShipStatus.Accepted)
            {
                var receiver = await _userManager.FindByIdAsync(currentUserId);
                await _notificationDispatcher.DispatchAsync(
                    dto.RequesterId,
                    currentUserId,
                    NotificationType.FriendRequest,
                    $"{receiver?.DisplayName ?? "Someone"} accepted your friend request.",
                    $"/Profile?userId={currentUserId}",
                    ct);
            }

            await _unitOfWork.CompleteAsync(ct);

            var responseDto = new FriendshipResponseDto(
                friendship.RequesterId,
                friendship.Requester?.DisplayName ?? string.Empty,
                friendship.Requester?.ProfilePictureUrl,
                friendship.ReceiverId,
                friendship.Receiver?.DisplayName ?? string.Empty,
                friendship.Receiver?.ProfilePictureUrl,
                friendship.Status,
                friendship.CreatedAt,
                friendship.UpdatedAt
            );

            return Result<FriendshipResponseDto>.Success(responseDto, $"Friend request {dto.Decision.ToString().ToLower()} successfully.");
        }

        public async Task<Result<IReadOnlyList<FriendSummaryDto>>> GetFriendsAsync(string userId, CancellationToken ct = default)
        {
            var friendships = await _unitOfWork.Friendships.GetUserFriendshipsByStatusAsync(userId, FriendShipStatus.Accepted, ct);

            var friends = friendships.Select(f =>
            {
                var isRequester = f.RequesterId == userId;
                var friendUser = isRequester ? f.Receiver : f.Requester;

                return new FriendSummaryDto(
                    friendUser.Id,
                    friendUser.DisplayName,
                    friendUser.UserName ?? friendUser.DisplayName,
                    friendUser.ProfilePictureUrl,
                    friendUser.Bio,
                    f.UpdatedAt ?? f.CreatedAt
                );
            }).ToList();

            return Result<IReadOnlyList<FriendSummaryDto>>.Success(friends);
        }

        public async Task<Result<IReadOnlyList<FriendBirthdayDto>>> GetFriendsBirthdaysTodayAsync(string userId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var friendships = await _unitOfWork.Friendships.GetUserFriendshipsByStatusAsync(userId, FriendShipStatus.Accepted, ct);

            var birthdays = friendships
                .Select(f => f.RequesterId == userId ? f.Receiver : f.Requester)
                .Where(u => u.DateOfBirth.Month == today.Month && u.DateOfBirth.Day == today.Day)
                .Select(u => new FriendBirthdayDto(u.Id, u.DisplayName, u.ProfilePictureUrl))
                .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Result<IReadOnlyList<FriendBirthdayDto>>.Success(birthdays);
        }

        public async Task<Result<IReadOnlyList<FriendshipResponseDto>>> GetPendingRequestsAsync(string currentUserId, CancellationToken ct = default)
        {
            var pendingFriendships = await _unitOfWork.Friendships.GetUserFriendshipsByStatusAsync(currentUserId, FriendShipStatus.Pending, ct);

            // Filter only those where current user is the RECEIVER (incoming requests)
            var incoming = pendingFriendships
                .Where(f => f.ReceiverId == currentUserId)
                .Select(f => new FriendshipResponseDto(
                    f.RequesterId,
                    f.Requester.DisplayName,
                    f.Requester.ProfilePictureUrl,
                    f.ReceiverId,
                    f.Receiver.DisplayName,
                    f.Receiver.ProfilePictureUrl,
                    f.Status,
                    f.CreatedAt,
                    f.UpdatedAt
                )).ToList();

            return Result<IReadOnlyList<FriendshipResponseDto>>.Success(incoming);
        }

        public async Task<Result<IReadOnlyList<FriendshipResponseDto>>> GetIncomingRequestHistoryAsync(string currentUserId, CancellationToken ct = default)
        {
            var friendships = await _unitOfWork.Friendships.GetIncomingFriendRequestHistoryAsync(currentUserId, ct);

            var history = friendships
                .Select(f => new FriendshipResponseDto(
                    f.RequesterId,
                    f.Requester.DisplayName,
                    f.Requester.ProfilePictureUrl,
                    f.ReceiverId,
                    f.Receiver.DisplayName,
                    f.Receiver.ProfilePictureUrl,
                    f.Status,
                    f.CreatedAt,
                    f.UpdatedAt
                )).ToList();

            return Result<IReadOnlyList<FriendshipResponseDto>>.Success(history);
        }

        public async Task<string> GetRelationshipStatusAsync(string currentUserId, string otherUserId, CancellationToken ct = default)
        {
            if (currentUserId == otherUserId)
                return "Self";

            var friendship = await _unitOfWork.Friendships.GetFriendshipAsync(currentUserId, otherUserId, ct);
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
    }
}
