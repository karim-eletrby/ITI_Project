using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record LoginRequestDto(
        [Required(ErrorMessage = "Username or email is required.")]
        string Login,

        [Required(ErrorMessage = "Password is required.")]
        string Password
    );
}
