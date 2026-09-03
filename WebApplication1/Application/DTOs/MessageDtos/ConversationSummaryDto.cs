namespace Application.DTOs.MessageDtos
{
    public record ConversationSummaryDto(
    string OtherUserId,
    string OtherUserName,
    string? OtherUserProfilePictureUrl,
    string LastMessage,
    DateTime LastMessageSentAt,
    int UnreadCount,
    bool IsRequest
);
}
