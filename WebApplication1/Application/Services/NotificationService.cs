using Application.Common;
using Application.DTOs.NotificationDtos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationService(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<Result<IReadOnlyList<NotificationDto>>> GetUserNotificationsAsync(string currentUserId, CancellationToken ct = default)
        {
            var notificationRepo = _unitOfWork.Repository<Notification, int>();
            var notifications = await notificationRepo.FindAsync(n => n.RecipientId == currentUserId, ct);

            var list = new List<NotificationDto>();

            foreach (var n in notifications.OrderByDescending(n => n.CreatedAt))
            {
                string? actorName = null;
                string? actorPhoto = null;

                if (!string.IsNullOrEmpty(n.TriggeredById))
                {
                    var actor = await _userManager.FindByIdAsync(n.TriggeredById);
                    actorName = actor?.DisplayName;
                    actorPhoto = actor?.ProfilePictureUrl;
                }

                list.Add(new NotificationDto(
                    n.Id,
                    n.RecipientId,
                    n.TriggeredById,
                    actorName,
                    actorPhoto,
                    n.Type,
                    n.Message,
                    n.TargetUrl,
                    n.IsRead,
                    n.CreatedAt
                ));
            }

            return Result<IReadOnlyList<NotificationDto>>.Success(list);
        }

        public async Task<Result<bool>> MarkNotificationAsReadAsync(int notificationId, string currentUserId, CancellationToken ct = default)
        {
            var notificationRepo = _unitOfWork.Repository<Notification, int>();
            var notification = await notificationRepo.GetByIdAsync(notificationId, ct);

            if (notification == null)
                throw new NotFoundException("Notification not found.");

            if (notification.RecipientId != currentUserId)
                throw new ForbiddenException("You cannot alter another user's notifications.");

            notification.IsRead = true;
            notificationRepo.Update(notification);
            await _unitOfWork.CompleteAsync(ct);

            return Result<bool>.Success(true, "Notification marked as read.");
        }

        public async Task<Result<bool>> MarkAllAsReadAsync(string currentUserId, CancellationToken ct = default)
        {
            var notificationRepo = _unitOfWork.Repository<Notification, int>();
            var unread = await notificationRepo.FindAsync(n => n.RecipientId == currentUserId && !n.IsRead, ct);

            foreach (var item in unread)
            {
                item.IsRead = true;
                notificationRepo.Update(item);
            }

            await _unitOfWork.CompleteAsync(ct);
            return Result<bool>.Success(true, "All notifications marked as read.");
        }
    }
}
