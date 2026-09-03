using Application.DTOs.NotificationDtos;
using Application.Hubs;
using Application.Interfaces;
using Application.Interfaces.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Application.Services;

public class RealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public RealtimeNotificationService(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushNotificationToUserAsync(string userId, NotificationDto notification, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId).ReceiveNotification(notification);
    }

    public async Task PushUnreadCountAsync(string userId, int unreadCount, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId).UpdateUnreadCount(unreadCount);
    }
}
