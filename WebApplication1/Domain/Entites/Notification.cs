using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entites
{
    public class Notification : BaseEntity<int>
    {
        public string RecipientId { get; set; } = string.Empty;
        public ApplicationUser Recipient { get; set; } = null!;

        public string? TriggeredById { get; set; }
        public ApplicationUser? TriggeredBy { get; set; }

        public NotificationType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TargetUrl { get; set; }
        public bool IsRead { get; set; } = false;
    }
}
