using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record RegisterRequestDto(
        [Required(ErrorMessage = "Username is required.")]
        [MinLength(3, ErrorMessage = "Username must be at least 3 characters.")]
        [MaxLength(30, ErrorMessage = "Username cannot exceed 30 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9._]+$", ErrorMessage = "Username can only contain letters, numbers, dots, and underscores.")]
        string Username,

        [Required(ErrorMessage = "Name is required.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        string Name,

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        string Email,

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        string Password,

        [Required(ErrorMessage = "Please confirm your password.")]
        string ConfirmPassword,

        [Required(ErrorMessage = "Birthday is required.")]
        DateOnly DateOfBirth,

        [MaxLength(500)]
        string? Bio
    );
}
