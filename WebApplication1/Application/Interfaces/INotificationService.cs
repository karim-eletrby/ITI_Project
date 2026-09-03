using Application.Common;
using Application.DTOs.NotificationDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface INotificationService
    {
        Task<Result<IReadOnlyList<NotificationDto>>> GetUserNotificationsAsync(string currentUserId, CancellationToken ct = default);
        Task<Result<bool>> MarkNotificationAsReadAsync(int notificationId, string currentUserId, CancellationToken ct = default);
        Task<Result<bool>> MarkAllAsReadAsync(string currentUserId, CancellationToken ct = default);
    }
}
