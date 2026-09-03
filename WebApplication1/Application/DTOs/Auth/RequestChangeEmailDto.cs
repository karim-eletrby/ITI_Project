using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record RequestChangeEmailDto(
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
        string NewEmail
    );
}
