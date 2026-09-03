using Application.Common;
using Application.DTOs.MessageDtos;

namespace Application.Interfaces
{
    public interface IChatService
    {
        Task<Result<MessageDto>> SendMessageAsync(string senderId, SendMessageDto dto, CancellationToken ct = default);
        Task<Result<IReadOnlyList<MessageDto>>> GetConversationAsync(string currentUserId, string otherUserId, CancellationToken ct = default);
        Task<Result<IReadOnlyList<ConversationSummaryDto>>> GetConversationsSummaryAsync(string currentUserId, CancellationToken ct = default);
        Task<Result<ConversationContextDto>> GetConversationContextAsync(string currentUserId, string otherUserId, CancellationToken ct = default);
        Task<Result<bool>> MarkAsReadAsync(string currentUserId, string senderId, CancellationToken ct = default);
    }
}
