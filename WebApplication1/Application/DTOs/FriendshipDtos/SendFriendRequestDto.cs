using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.FriendshipDtos
{
    public record SendFriendRequestDto(
     [Required] 
    string ReceiverId
 );
}
