using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Auth
{
    public record AuthResponseDto(
     string UserId,
     string DisplayName,
     string Email,
     string? ProfilePictureUrl,
     string AccessToken,
     string RefreshToken,
     DateTime RefreshTokenExpiresOn
 );
}
