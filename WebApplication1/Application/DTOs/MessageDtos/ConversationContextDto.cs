namespace Application.DTOs.MessageDtos;

public record ConversationContextDto(
    string OtherUserId,
    string OtherUserDisplayName,
    string? OtherUserProfilePictureUrl,
    bool IsFriend,
    bool CanSendMessage,
    string? SendBlockedReason,
    bool IsRequestConversation,
    string RelationshipStatus,
    bool IsIncomingRequest,
    bool IsOutgoingRequest
);
