using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    public record UpdateProfileDto(
        [MinLength(2), MaxLength(100)]
        string? DisplayName,
        [MinLength(3), MaxLength(30), RegularExpression(@"^[a-zA-Z0-9._]+$")]
        string? Username,
        [MaxLength(500)] string? Bio,
        [MaxLength(500)] string? ProfilePictureUrl,
        [MaxLength(500)] string? CoverPictureUrl
    );
}
