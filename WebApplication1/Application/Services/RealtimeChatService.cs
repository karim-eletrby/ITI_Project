using Application.DTOs.MessageDtos;
using Application.Hubs;
using Application.Interfaces;
using Application.Interfaces.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Application.Services;

public class RealtimeChatService : IRealtimeChatService
{
    private readonly IHubContext<ChatHub, IChatClient> _hubContext;

    public RealtimeChatService(IHubContext<ChatHub, IChatClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushMessageToUserAsync(string userId, MessageDto message, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId).ReceiveMessage(message);
    }
}
