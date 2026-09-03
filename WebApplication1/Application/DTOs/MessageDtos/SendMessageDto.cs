using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.MessageDtos
{
    public record SendMessageDto(
     [Required]
    string ReceiverId,
    [MaxLength(2000)]
        string Content,
        int? SharedPostId = null
);
}
