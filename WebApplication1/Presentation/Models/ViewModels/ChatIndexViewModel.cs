using Application.DTOs.MessageDtos;

namespace WebApplication1.Models.ViewModels;

public class ChatIndexViewModel
{
    public IReadOnlyList<ConversationSummaryDto> Conversations { get; set; } = Array.Empty<ConversationSummaryDto>();
    public IReadOnlyList<MessageDto> ActiveMessages { get; set; } = Array.Empty<MessageDto>();
    public string? ActiveOtherUserId { get; set; }
    public string? CurrentUserId { get; set; }
}
