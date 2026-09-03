
using Application.DTOs.NotificationDtos;

namespace Application.Interfaces.Hubs
{
    public interface INotificationClient
    {
        Task ReceiveNotification(NotificationDto notification);
        Task UpdateUnreadCount(int unreadCount);
    }
}
