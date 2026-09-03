using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;


namespace Domain.Entites
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }

        [MaxLength(500)]
        public string? ProfilePictureUrl { get; set; }

        [MaxLength(500)]
        public string? CoverPictureUrl { get; set; }

        public DateOnly DateOfBirth { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // JWT Security
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        // Social Graph & Navigation Collections
        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<PostLikes> PostLikes { get; set; } = new List<PostLikes>();
        public ICollection<Friendship> SentFriendRequests { get; set; } = new List<Friendship>();
        public ICollection<Friendship> ReceivedFriendRequests { get; set; } = new List<Friendship>();
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<Notification> ReceivedNotifications { get; set; } = new List<Notification>();
        public ICollection<Notification> TriggeredNotifications { get; set; } = new List<Notification>();
    }
}
