using Application.DTOs.PostsDtos;

namespace Application.DTOs.MessageDtos
{
    public record MessageDto(
     int Id,
     string SenderId,
     string SenderName,
     string? SenderProfilePictureUrl,
     string ReceiverId,
     string ReceiverName,
     string? ReceiverProfilePictureUrl,
     string Content,
     bool IsRequest,
     bool IsRead,
     DateTime? ReadAt,
     DateTime SentAt,
     SharedPostPreviewDto? SharedPost = null
 );
}
