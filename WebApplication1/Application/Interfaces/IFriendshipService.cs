using Application.Common;
using Application.DTOs.FriendshipDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IFriendshipService
    {
        Task<Result<FriendshipResponseDto>> SendRequestAsync(string requesterId, SendFriendRequestDto dto, CancellationToken ct = default);
        Task<Result<FriendshipResponseDto>> RespondToRequestAsync(string currentUserId, RespondFriendRequestDto dto, CancellationToken ct = default);
        Task<Result<IReadOnlyList<FriendSummaryDto>>> GetFriendsAsync(string userId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<FriendBirthdayDto>>> GetFriendsBirthdaysTodayAsync(string userId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<FriendshipResponseDto>>> GetPendingRequestsAsync(string currentUserId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<FriendshipResponseDto>>> GetIncomingRequestHistoryAsync(string currentUserId, CancellationToken ct = default);
        Task<string> GetRelationshipStatusAsync(string currentUserId, string otherUserId, CancellationToken ct = default);
    }
}
