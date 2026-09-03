using Application.Common;
using Application.DTOs.Auth;

namespace Application.Interfaces
{
    public interface IAuthService
    {
        Task<Result<RegisterPendingResponseDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
        Task<Result<AuthResponseDto>> VerifyEmailAsync(VerifyEmailOtpDto dto, CancellationToken ct = default);
        Task<Result<OtpSendResponseDto>> ResendVerificationOtpAsync(ResendEmailOtpDto dto, CancellationToken ct = default);
        Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
        Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
        Task<Result<bool>> RevokeTokenAsync(RevokeTokenRequestDto dto, CancellationToken ct = default);
        Task<Result<UserProfileDto>> GetProfileAsync(string userId, CancellationToken ct = default);
        Task<Result<UserProfileDto>> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken ct = default);
        Task<Result<OtpSendResponseDto>> RequestChangeEmailAsync(string userId, RequestChangeEmailDto dto, CancellationToken ct = default);
        Task<Result<UserProfileDto>> ConfirmChangeEmailAsync(string userId, VerifyChangeEmailDto dto, CancellationToken ct = default);
        Task<Result<ForgotPasswordResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken ct = default);
        Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken ct = default);
    }
}
