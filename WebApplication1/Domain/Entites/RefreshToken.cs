using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entites
{
    public class RefreshToken : BaseEntity<int>
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresOn { get; set; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
        public DateTime? RevokedOn { get; set; }
        public bool IsActive => RevokedOn is null && !IsExpired;

        // Foreign Key to ApplicationUser
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
    }
}
