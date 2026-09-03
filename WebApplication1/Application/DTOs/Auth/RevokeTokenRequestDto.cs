
using System.ComponentModel.DataAnnotations;


namespace Application.DTOs.Auth
{
    public record RevokeTokenRequestDto(
     [Required] 
    string RefreshToken
);
}
