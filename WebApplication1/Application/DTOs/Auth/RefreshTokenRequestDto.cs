using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record RefreshTokenRequestDto(
        [Required] 
    string RefreshToken
);
}
