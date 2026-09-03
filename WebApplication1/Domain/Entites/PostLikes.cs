using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entites
{
    public class PostLikes
    {
        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
