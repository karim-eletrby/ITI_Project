using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entites
{
    public class Message : BaseEntity<int>
    {
        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser Sender { get; set; } = null!;

        public string ReceiverId { get; set; } = string.Empty;
        public ApplicationUser Receiver { get; set; } = null!;

        public string Content { get; set; } = string.Empty;
        public int? SharedPostId { get; set; }
        public Post? SharedPost { get; set; }
        public bool IsRequest { get; set; } = false;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}
