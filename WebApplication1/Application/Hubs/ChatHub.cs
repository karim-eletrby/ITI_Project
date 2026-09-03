using Application.DTOs.MessageDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Application.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    private string CurrentUserId =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("User identity not found.");

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CurrentUserId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CurrentUserId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendDirectMessage(SendMessageDto dto)
    {
        try
        {
            var result = await _chatService.SendMessageAsync(CurrentUserId, dto);
            if (!result.IsSuccess)
                throw new HubException(result.Message ?? "Failed to send message.");
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task SendTypingIndicator(string receiverId)
    {
        await Clients.Group(receiverId).UserTyping(CurrentUserId);
    }

    public async Task SendStoppedTypingIndicator(string receiverId)
    {
        await Clients.Group(receiverId).UserStoppedTyping(CurrentUserId);
    }
}
