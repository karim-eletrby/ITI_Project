using Application.Common;
using Application.DTOs.Auth;
using Domain.Entites;

namespace Application.Interfaces
{
    public interface IEmailOtpService
    {
        Task<Result<OtpSendResponseDto>> SendRegistrationOtpAsync(ApplicationUser user, CancellationToken ct = default);
        Task<Result<OtpSendResponseDto>> ResendRegistrationOtpAsync(string email, CancellationToken ct = default);
        Task<Result<OtpSendResponseDto>> SendForgotPasswordOtpAsync(ApplicationUser user, CancellationToken ct = default);
        Task<Result<OtpSendResponseDto>> SendChangeEmailOtpAsync(string userId, string newEmail, CancellationToken ct = default);
        Task ValidateRegistrationOtpAsync(ApplicationUser user, string code, CancellationToken ct = default);
        Task ValidateForgotPasswordOtpAsync(ApplicationUser user, string code, CancellationToken ct = default);
        Task ValidateChangeEmailOtpAsync(string userId, string newEmail, string code, CancellationToken ct = default);
        Task EnsureEmailAvailableForUserAsync(ApplicationUser user, string normalizedEmail, CancellationToken ct = default);
    }
}
