using Domain.Common;
using Domain.Enums;

namespace Domain.Entites
{
    public class EmailOtp : BaseEntity<int>
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        /// <summary>Primary email context (registration inbox, account email for forgot password).</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Proposed new address for <see cref="OtpPurpose.EmailChange"/> only.</summary>
        public string? TargetEmail { get; set; }

        public string CodeHash { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AttemptCount { get; set; }
        public bool IsUsed { get; set; }
    }
}
