using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record VerifyChangeEmailDto(
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
        string NewEmail,

        [Required(ErrorMessage = "Verification code is required.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit verification code.")]
        string Code
    );
}
