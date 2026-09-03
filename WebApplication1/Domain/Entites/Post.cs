using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Domain.Entites
{
    public class Post : BaseEntity<int>
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public string Content { get; set; } = string.Empty;
        public string? MediaUrl { get; set; }
        public PostPrivacy Privacy { get; set; } = PostPrivacy.Public;

        public int? SharedPostId { get; set; }
        public Post? SharedPost { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<PostLikes> Likes { get; set; } = new List<PostLikes>();
    }
}
