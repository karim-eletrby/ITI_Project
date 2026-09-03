namespace Application.DTOs.Auth;

using System.ComponentModel.DataAnnotations;

public record ForgotPasswordRequestDto(
    [Required(ErrorMessage = "Username or email is required.")]
    string Login
);

public record ResetPasswordRequestDto(
    [Required(ErrorMessage = "Username or email is required.")]
    string Login,

    [Required(ErrorMessage = "Verification code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit verification code.")]
    string Code,

    [Required, MinLength(6)]
    string NewPassword
);

public record ForgotPasswordResponseDto(
    string Message,
    bool EmailSent
);
