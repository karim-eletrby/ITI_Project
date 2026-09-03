using Application.DTOs.NotificationDtos;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Application.Services
{
    public class NotificationDispatcher : INotificationDispatcher
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRealtimeNotificationService _realtime;

        public NotificationDispatcher(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IRealtimeNotificationService realtime)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _realtime = realtime;
        }

        public async Task DispatchAsync(
            string recipientId,
            string? triggeredById,
            NotificationType type,
            string message,
            string? targetUrl,
            CancellationToken ct = default)
        {
            if (recipientId == triggeredById)
                return;

            var notification = new Notification
            {
                RecipientId = recipientId,
                TriggeredById = triggeredById,
                Type = type,
                Message = message,
                TargetUrl = targetUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            var notificationRepo = _unitOfWork.Repository<Notification, int>();
            await notificationRepo.AddAsync(notification, ct);
            await _unitOfWork.CompleteAsync(ct);

            string? actorName = null;
            string? actorPhoto = null;
            if (!string.IsNullOrEmpty(triggeredById))
            {
                var actor = await _userManager.FindByIdAsync(triggeredById);
                actorName = actor?.DisplayName;
                actorPhoto = actor?.ProfilePictureUrl;
            }

            var dto = new NotificationDto(
                notification.Id,
                recipientId,
                triggeredById,
                actorName,
                actorPhoto,
                type,
                message,
                targetUrl,
                false,
                notification.CreatedAt
            );

            await _realtime.PushNotificationToUserAsync(recipientId, dto, ct);

            var unreadNotifications = await notificationRepo.FindAsync(
                n => n.RecipientId == recipientId && !n.IsRead, ct);
            await _realtime.PushUnreadCountAsync(recipientId, unreadNotifications.Count, ct);
        }
    }
}
