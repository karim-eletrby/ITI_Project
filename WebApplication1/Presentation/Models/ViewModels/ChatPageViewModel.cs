using Application.DTOs.MessageDtos;

namespace WebApplication1.Models.ViewModels;

public class ChatPageViewModel
{
    public IReadOnlyList<ConversationSummaryDto> Conversations { get; set; } = [];
    public IReadOnlyList<MessageDto> Messages { get; set; } = [];
    public string? ActiveUserId { get; set; }
    public string? ActiveUserName { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public bool ActiveIsRequest { get; set; }
    public bool IsIncomingMessageRequest { get; set; }
    public bool IsOutgoingMessageRequest { get; set; }
    public bool CanSendMessage { get; set; } = true;
    public string? SendBlockedReason { get; set; }
    public string RelationshipStatus { get; set; } = "None";
}
