using Application.DTOs.Chat;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IChatRealtimeService
    {
        Task SendDirectMessageAsync(string receiverId, ChatMessageDto message, CancellationToken ct = default);
        Task NotifyUserTypingAsync(string receiverId, string senderId, bool isTyping, CancellationToken ct = default);
    }
}
