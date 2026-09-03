using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Chat
{
    public record ChatMessageDto(
    int Id,
    string SenderId,
    string SenderName,
    string? SenderProfilePictureUrl,
    string ReceiverId,
    string Content,
    DateTime SentAt,
    bool IsRead
);
}
