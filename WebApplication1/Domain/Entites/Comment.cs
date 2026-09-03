using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entites
{
    public class Comment : BaseEntity<int>
    {
        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }
        public Comment? ParentComment { get; set; }
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
