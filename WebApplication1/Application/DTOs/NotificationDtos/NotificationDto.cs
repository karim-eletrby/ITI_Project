using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.NotificationDtos
{
    public record NotificationDto(
     int Id,
     string RecipientId,
     string? TriggeredById,
     string? TriggeredByName,
     string? TriggeredByProfilePictureUrl,
     NotificationType Type,
     string Message,
     string? TargetUrl,
     bool IsRead,
     DateTime CreatedAt
 );
}
