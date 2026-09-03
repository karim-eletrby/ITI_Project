using Application.Common;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly IEmailOtpService _emailOtpService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            IUnitOfWork unitOfWork,
            IEmailSender emailSender,
            IEmailOtpService emailOtpService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _emailOtpService = emailOtpService;
        }

        public async Task<Result<RegisterPendingResponseDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            var email = dto.Email.Trim();
            var name = dto.Name.Trim();

            if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["confirmPassword"] = ["Passwords do not match."]
                });
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (!existingUser.EmailConfirmed)
                {
                    var resend = await _emailOtpService.SendRegistrationOtpAsync(existingUser, ct);
                    if (!resend.IsSuccess || resend.Data is null)
                        throw new BadRequestException(resend.Message ?? "Could not send verification code.");

                    return Result<RegisterPendingResponseDto>.Success(
                        new RegisterPendingResponseDto(
                            existingUser.Id,
                            existingUser.Email!,
                            resend.Data.EmailSent),
                        "Account exists but email is not verified. We sent a new verification code.");
                }

                throw new ConflictException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["email"] = ["This email is already registered."]
                });
            }

            string username;
            try
            {
                username = UsernameValidator.Normalize(dto.Username);
                UsernameValidator.Validate(username);
            }
            catch (ArgumentException ex)
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["username"] = [ex.Message]
                });
            }

            if (await _userManager.FindByNameAsync(username) != null)
            {
                throw new ConflictException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["username"] = ["This username is already taken."]
                });
            }

            var user = new ApplicationUser
            {
                UserName = username,
                DisplayName = name,
                Email = email,
                EmailConfirmed = false,
                DateOfBirth = dto.DateOfBirth,
                Bio = dto.Bio,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var fieldErrors = IdentityErrorMapper.MapRegistrationErrors(result.Errors);
                throw new BadRequestException("Please fix the errors below.", fieldErrors);
            }

            var otpResult = await _emailOtpService.SendRegistrationOtpAsync(user, ct);
            if (!otpResult.IsSuccess || otpResult.Data is null)
                throw new BadRequestException(otpResult.Message ?? "Could not send verification code.");

            var pending = new RegisterPendingResponseDto(
                user.Id,
                user.Email!,
                otpResult.Data.EmailSent);

            return Result<RegisterPendingResponseDto>.Success(
                pending,
                "Account created. Enter the verification code sent to your email.");
        }

        public async Task<Result<AuthResponseDto>> VerifyEmailAsync(VerifyEmailOtpDto dto, CancellationToken ct = default)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
                throw new NotFoundException("No account found with that email.");

            if (user.EmailConfirmed)
                throw new BadRequestException("This email is already verified. You can sign in.");

            await _emailOtpService.ValidateRegistrationOtpAsync(user, dto.Code, ct);

            user.EmailConfirmed = true;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = updateResult.Errors.Select(e => e.Description);
                throw new BadRequestException("Could not verify email.", errors);
            }

            return Result<AuthResponseDto>.Success(
                await IssueAuthResponseAsync(user, ct),
                "Email verified successfully.");
        }

        public async Task<Result<OtpSendResponseDto>> ResendVerificationOtpAsync(ResendEmailOtpDto dto, CancellationToken ct = default)
        {
            var result = await _emailOtpService.ResendRegistrationOtpAsync(dto.Email, ct);
            return result;
        }

        public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await UserAccountLookup.FindByLoginAsync(_userManager, dto.Login, ct);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                // Expected for wrong credentials — handled by GlobalExceptionHandler → 401 JSON (not a crash).
                throw new UnauthorizedException("Sign in failed.", new Dictionary<string, string[]>
                {
                    ["login"] = ["Invalid username/email or password."],
                    ["password"] = ["Invalid username/email or password."]
                });
            }

            if (!user.EmailConfirmed)
            {
                var verifyMessage = "Please verify your email before signing in.";

                try
                {
                    await _emailOtpService.SendRegistrationOtpAsync(user, ct);
                    verifyMessage += " We sent a new verification code to your inbox.";
                }
                catch (BadRequestException ex) when (ex.Message.Contains("wait", StringComparison.OrdinalIgnoreCase))
                {
                    verifyMessage += " Check your inbox for the verification code we already sent.";
                }

                throw new UnauthorizedException(
                    "Sign in failed.",
                    new Dictionary<string, string[]>
                    {
                        ["email"] = [verifyMessage]
                    },
                    new { pendingEmail = user.Email });
            }

            var response = await IssueAuthResponseAsync(user, ct);
            return Result<AuthResponseDto>.Success(response, "Logged in successfully.");
        }

        private async Task<AuthResponseDto> IssueAuthResponseAsync(ApplicationUser user, CancellationToken ct)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.GenerateAccessToken(user, roles);
            var refreshToken = _tokenService.GenerateRefreshToken(user.Id);

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken, ct);
            await _unitOfWork.CompleteAsync(ct);

            return new AuthResponseDto(
                user.Id,
                user.DisplayName,
                user.Email!,
                user.ProfilePictureUrl,
                accessToken,
                refreshToken.Token,
                refreshToken.ExpiresOn
            );
        }

        public async Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default)
        {
            var existingToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(dto.RefreshToken, ct);

            if (existingToken == null)
                throw new UnauthorizedException("Invalid refresh token.", "INVALID_TOKEN");

            if (!existingToken.IsActive)
                throw new UnauthorizedException("Refresh token is expired or revoked.", "EXPIRED_OR_REVOKED_TOKEN");

            // Revoke the old refresh token
            existingToken.RevokedOn = DateTime.UtcNow;
            _unitOfWork.RefreshTokens.Update(existingToken);

            var user = existingToken.User;
            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
            var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id);

            await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken, ct);
            await _unitOfWork.CompleteAsync(ct);

            var response = new AuthResponseDto(
                user.Id,
                user.DisplayName,
                user.Email!,
                user.ProfilePictureUrl,
                newAccessToken,
                newRefreshToken.Token,
                newRefreshToken.ExpiresOn
            );

            return Result<AuthResponseDto>.Success(response, "Token refreshed successfully.");
        }

        public async Task<Result<bool>> RevokeTokenAsync(RevokeTokenRequestDto dto, CancellationToken ct = default)
        {
            var token = await _unitOfWork.RefreshTokens.GetByTokenAsync(dto.RefreshToken, ct);

            if (token == null)
                throw new NotFoundException("Token not found.", "TOKEN_NOT_FOUND");

            if (!token.IsActive)
                throw new BadRequestException("Token is already inactive.", "TOKEN_ALREADY_INACTIVE");

            token.RevokedOn = DateTime.UtcNow;
            _unitOfWork.RefreshTokens.Update(token);
            await _unitOfWork.CompleteAsync(ct);

            return Result<bool>.Success(true, "Token revoked successfully.");
        }

        public async Task<Result<UserProfileDto>> GetProfileAsync(string userId, CancellationToken ct = default)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
                throw new NotFoundException("User profile not found.", "USER_NOT_FOUND");

            var friends = await _unitOfWork.Friendships.GetUserFriendshipsByStatusAsync(userId, FriendShipStatus.Accepted, ct);
            var postsCount = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Posts.Count)
                .FirstAsync(ct);

            var profileDto = new UserProfileDto(
                user.Id,
                user.DisplayName,
                user.UserName ?? user.DisplayName,
                user.Email!,
                user.EmailConfirmed,
                user.Bio,
                user.ProfilePictureUrl,
                user.CoverPictureUrl,
                user.DateOfBirth,
                friends.Count,
                postsCount,
                user.CreatedAt
            );

            return Result<UserProfileDto>.Success(profileDto);
        }

        public async Task<Result<UserProfileDto>> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("User profile not found.", "USER_NOT_FOUND");

            var updatingDisplayName = !string.IsNullOrWhiteSpace(dto.DisplayName);
            var updatingUsername = !string.IsNullOrWhiteSpace(dto.Username);
            var updatingBio = dto.Bio != null;
            var updatingMedia = dto.ProfilePictureUrl != null || dto.CoverPictureUrl != null;

            // Picture-only updates bypass UserManager validation so legacy accounts
            // with invalid UserName values (e.g. spaces from old migrations) still work.
            if (!updatingDisplayName && !updatingUsername && !updatingBio && updatingMedia)
            {
                if (dto.ProfilePictureUrl != null)
                {
                    await _userManager.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(u => u.ProfilePictureUrl, dto.ProfilePictureUrl),
                            ct);
                }

                if (dto.CoverPictureUrl != null)
                {
                    await _userManager.Users
                        .Where(u => u.Id == userId)
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(u => u.CoverPictureUrl, dto.CoverPictureUrl),
                            ct);
                }

                return await GetProfileAsync(userId, ct);
            }

            if (updatingDisplayName)
                user.DisplayName = dto.DisplayName!.Trim();

            if (updatingUsername)
            {
                string username;
                try
                {
                    username = UsernameValidator.Normalize(dto.Username);
                    UsernameValidator.Validate(username);
                }
                catch (ArgumentException ex)
                {
                    throw new BadRequestException(ex.Message);
                }

                if (!string.Equals(username, user.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    var taken = await _userManager.FindByNameAsync(username);
                    if (taken != null && taken.Id != userId)
                        throw new ConflictException("Username is already taken.", "USERNAME_TAKEN");

                    user.UserName = username;
                }
            }

            if (dto.Bio != null)
                user.Bio = dto.Bio;

            if (dto.ProfilePictureUrl != null)
                user.ProfilePictureUrl = dto.ProfilePictureUrl;

            if (dto.CoverPictureUrl != null)
                user.CoverPictureUrl = dto.CoverPictureUrl;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                throw new BadRequestException("Failed to update profile.", errors);
            }

            return await GetProfileAsync(userId, ct);
        }

        public async Task<Result<OtpSendResponseDto>> RequestChangeEmailAsync(
            string userId,
            RequestChangeEmailDto dto,
            CancellationToken ct = default)
        {
            return await _emailOtpService.SendChangeEmailOtpAsync(userId, dto.NewEmail, ct);
        }

        public async Task<Result<UserProfileDto>> ConfirmChangeEmailAsync(
            string userId,
            VerifyChangeEmailDto dto,
            CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                throw new NotFoundException("User profile not found.", "USER_NOT_FOUND");

            var newEmail = EmailAddressValidator.NormalizeOrThrow(dto.NewEmail);
            var oldEmail = user.Email;
            await _emailOtpService.EnsureEmailAvailableForUserAsync(user, newEmail, ct);
            await _emailOtpService.ValidateChangeEmailOtpAsync(userId, newEmail, dto.Code, ct);

            var setEmailResult = await _userManager.SetEmailAsync(user, newEmail);
            if (!setEmailResult.Succeeded)
            {
                var errors = setEmailResult.Errors.Select(e => e.Description);
                throw new BadRequestException("Could not update email.", errors);
            }

            user.EmailConfirmed = true;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = updateResult.Errors.Select(e => e.Description);
                throw new BadRequestException("Could not update email.", errors);
            }

            await RevokeAllRefreshTokensAsync(userId, ct);

            if (!string.IsNullOrWhiteSpace(oldEmail) &&
                !string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var htmlBody = $"""
                        <p>Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
                        <p>Your Connectly account email was changed to <strong>{System.Net.WebUtility.HtmlEncode(newEmail)}</strong>.</p>
                        <p>If you did not make this change, contact support immediately and reset your password.</p>
                        """;
                    await _emailSender.SendAsync(oldEmail, "Your Connectly email was changed", htmlBody, ct);
                }
                catch
                {
                    // Email change already committed; do not fail the request if courtesy mail cannot be sent.
                }
            }

            return await GetProfileAsync(userId, ct);
        }

        public async Task<Result<ForgotPasswordResponseDto>> ForgotPasswordAsync(
            ForgotPasswordRequestDto dto,
            CancellationToken ct = default)
        {
            const string successMessage =
                "If an account with that username or email exists, we sent a verification code.";

            var user = await UserAccountLookup.FindByLoginAsync(_userManager, dto.Login, ct);
            if (user is null)
            {
                return Result<ForgotPasswordResponseDto>.Success(
                    new ForgotPasswordResponseDto(successMessage, false),
                    successMessage);
            }

            var otpResult = await _emailOtpService.SendForgotPasswordOtpAsync(user, ct);
            return Result<ForgotPasswordResponseDto>.Success(
                new ForgotPasswordResponseDto(otpResult.Message, otpResult.Data?.EmailSent ?? true),
                otpResult.Message);
        }

        public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken ct = default)
        {
            var user = await UserAccountLookup.FindByLoginAsync(_userManager, dto.Login, ct);
            if (user is null)
                throw new BadRequestException("Invalid verification code or account.");

            await _emailOtpService.ValidateForgotPasswordOtpAsync(user, dto.Code, ct);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                throw new BadRequestException("Could not reset your password.", errors);
            }

            await RevokeAllRefreshTokensAsync(user.Id, ct);

            return Result<bool>.Success(true, "Password reset successfully. You can now sign in.");
        }

        private async Task RevokeAllRefreshTokensAsync(string userId, CancellationToken ct)
        {
            var activeTokens = await _unitOfWork.RefreshTokens.FindAsync(
                t => t.UserId == userId && t.RevokedOn == null, ct);

            var revokedAny = false;
            foreach (var token in activeTokens.Where(t => !t.IsExpired))
            {
                token.RevokedOn = DateTime.UtcNow;
                _unitOfWork.RefreshTokens.Update(token);
                revokedAny = true;
            }

            if (revokedAny)
                await _unitOfWork.CompleteAsync(ct);
        }
    }
}
