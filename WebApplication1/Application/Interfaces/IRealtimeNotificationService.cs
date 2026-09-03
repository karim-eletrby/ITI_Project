using Application.DTOs.NotificationDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IRealtimeNotificationService
    {
        Task PushNotificationToUserAsync(string userId, NotificationDto notification, CancellationToken ct = default);
        Task PushUnreadCountAsync(string userId, int unreadCount, CancellationToken ct = default);
    }
}
