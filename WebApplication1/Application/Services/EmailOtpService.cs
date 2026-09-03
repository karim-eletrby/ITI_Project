using Application.Common;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Entites;
using Domain.Enums;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Application.Services
{
    public class EmailOtpService : IEmailOtpService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly OtpOptions _otpOptions;
        private readonly SmtpSettings _smtpSettings;

        public EmailOtpService(
            IUnitOfWork unitOfWork,
            IEmailSender emailSender,
            UserManager<ApplicationUser> userManager,
            IOptions<OtpOptions> otpOptions,
            IOptions<SmtpSettings> smtpSettings)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _userManager = userManager;
            _otpOptions = otpOptions.Value;
            _smtpSettings = smtpSettings.Value;
        }

        public Task<Result<OtpSendResponseDto>> SendRegistrationOtpAsync(ApplicationUser user, CancellationToken ct = default)
            => SendOtpAsync(
                user,
                user.Email!,
                OtpPurpose.Registration,
                targetEmail: null,
                deliveryEmail: user.Email!,
                "Verify your Connectly account",
                ct);

        public async Task<Result<OtpSendResponseDto>> ResendRegistrationOtpAsync(string email, CancellationToken ct = default)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(normalized);
            if (user is null)
                throw new NotFoundException("No account found with that email.");

            if (user.EmailConfirmed)
                throw new BadRequestException("This email is already verified. You can sign in.");

            return await SendRegistrationOtpAsync(user, ct);
        }

        public Task<Result<OtpSendResponseDto>> SendForgotPasswordOtpAsync(ApplicationUser user, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new BadRequestException(
                    "This account has no email on file. Contact support to reset your password.");
            }

            return SendOtpAsync(
                user,
                user.Email!,
                OtpPurpose.ForgotPassword,
                targetEmail: null,
                deliveryEmail: user.Email!,
                "Reset your Connectly password",
                ct);
        }

        public async Task<Result<OtpSendResponseDto>> SendChangeEmailOtpAsync(string userId, string newEmail, CancellationToken ct = default)
        {
            var normalizedTarget = EmailAddressValidator.NormalizeOrThrow(newEmail);

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                throw new NotFoundException("User not found.");

            await EnsureEmailAvailableForUserAsync(user, normalizedTarget, ct);

            return await SendOtpAsync(
                user,
                user.Email!,
                OtpPurpose.EmailChange,
                targetEmail: normalizedTarget,
                deliveryEmail: normalizedTarget,
                "Confirm your new Connectly email",
                ct);
        }

        public async Task EnsureEmailAvailableForUserAsync(ApplicationUser user, string normalizedEmail, CancellationToken ct = default)
        {
            var normalizedKey = _userManager.NormalizeEmail(normalizedEmail);
            var currentKey = !string.IsNullOrWhiteSpace(user.NormalizedEmail)
                ? user.NormalizedEmail
                : _userManager.NormalizeEmail(user.Email ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(currentKey) &&
                string.Equals(currentKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["newEmail"] = ["That is already your current email address."]
                });
            }

            var emailTaken = await _userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.NormalizedEmail == normalizedKey && u.Id != user.Id, ct);

            if (emailTaken)
            {
                throw new BadRequestException("This email is already registered on Connectly.", new Dictionary<string, string[]>
                {
                    ["newEmail"] = ["This email is already registered on Connectly. Choose a different address."]
                });
            }
        }

        public Task ValidateRegistrationOtpAsync(ApplicationUser user, string code, CancellationToken ct = default)
            => ValidateOtpAsync(user.Id, user.Email!, OtpPurpose.Registration, targetEmail: null, code, ct);

        public Task ValidateForgotPasswordOtpAsync(ApplicationUser user, string code, CancellationToken ct = default)
            => ValidateOtpAsync(user.Id, user.Email!, OtpPurpose.ForgotPassword, targetEmail: null, code, ct);

        public async Task ValidateChangeEmailOtpAsync(string userId, string newEmail, string code, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
                throw new NotFoundException("User not found.");

            var normalizedTarget = newEmail.Trim().ToLowerInvariant();
            var accountEmail = user.Email.Trim().ToLowerInvariant();
            await ValidateOtpAsync(userId, accountEmail, OtpPurpose.EmailChange, normalizedTarget, code, ct);
        }

        private void EnsureSmtpConfigured()
        {
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) ||
                string.IsNullOrWhiteSpace(_smtpSettings.SenderEmail) ||
                string.IsNullOrWhiteSpace(_smtpSettings.SenderPassword))
            {
                throw new BadRequestException(
                    "Email delivery is not configured. Set Smtp credentials via user secrets or environment configuration.");
            }
        }

        private async Task<Result<OtpSendResponseDto>> SendOtpAsync(
            ApplicationUser user,
            string email,
            OtpPurpose purpose,
            string? targetEmail,
            string deliveryEmail,
            string subject,
            CancellationToken ct)
        {
            EnsureSmtpConfigured();

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedTarget = targetEmail?.Trim().ToLowerInvariant();
            var normalizedDelivery = deliveryEmail.Trim().ToLowerInvariant();
            var otpRepo = _unitOfWork.Repository<EmailOtp, int>();

            var existing = await otpRepo.FindAsync(o =>
                o.UserId == user.Id &&
                o.Purpose == purpose &&
                !o.IsUsed &&
                (purpose != OtpPurpose.EmailChange || o.TargetEmail == normalizedTarget), ct);

            var latest = existing.OrderByDescending(o => o.CreatedAt).FirstOrDefault();
            if (latest is not null)
            {
                var cooldown = TimeSpan.FromSeconds(_otpOptions.ResendCooldownSeconds);
                if (DateTime.UtcNow - latest.CreatedAt < cooldown)
                {
                    var waitSeconds = (int)Math.Ceiling(cooldown.TotalSeconds - (DateTime.UtcNow - latest.CreatedAt).TotalSeconds);
                    throw new BadRequestException($"Please wait {waitSeconds} seconds before requesting a new code.");
                }

                foreach (var old in existing.Where(o => !o.IsUsed))
                {
                    old.IsUsed = true;
                    otpRepo.Update(old);
                }
            }

            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            var otp = new EmailOtp
            {
                UserId = user.Id,
                Email = normalizedEmail,
                TargetEmail = normalizedTarget,
                CodeHash = OtpHasher.Hash(code, normalizedEmail, purpose, normalizedTarget, _otpOptions.Pepper),
                Purpose = purpose,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                AttemptCount = 0,
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await otpRepo.AddAsync(otp, ct);
            await _unitOfWork.CompleteAsync(ct);

            var htmlBody = BuildOtpEmailBody(user.DisplayName, code, purpose);
            await _emailSender.SendAsync(normalizedDelivery, subject, htmlBody, ct);

            const string message = "Verification code sent. Check your inbox and spam folder.";
            return Result<OtpSendResponseDto>.Success(new OtpSendResponseDto(true, message), message);
        }

        private async Task ValidateOtpAsync(
            string userId,
            string email,
            OtpPurpose purpose,
            string? targetEmail,
            string code,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6 || !code.Trim().All(char.IsDigit))
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["code"] = ["Enter the 6-digit verification code."]
                });
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedTarget = targetEmail?.Trim().ToLowerInvariant();
            var otpRepo = _unitOfWork.Repository<EmailOtp, int>();

            var activeOtps = await otpRepo.FindAsync(o =>
                o.UserId == userId &&
                o.Purpose == purpose &&
                !o.IsUsed &&
                (purpose != OtpPurpose.EmailChange || o.TargetEmail == normalizedTarget), ct);

            var otp = activeOtps.OrderByDescending(o => o.CreatedAt).FirstOrDefault();
            if (otp is null || otp.ExpiresAt < DateTime.UtcNow)
                throw new BadRequestException("Verification code expired. Request a new one.");

            if (otp.AttemptCount >= _otpOptions.MaxAttempts)
            {
                otp.IsUsed = true;
                otpRepo.Update(otp);
                await _unitOfWork.CompleteAsync(ct);
                throw new BadRequestException("Too many failed attempts. Request a new verification code.");
            }

            var hash = OtpHasher.Hash(code.Trim(), normalizedEmail, purpose, normalizedTarget, _otpOptions.Pepper);
            if (!string.Equals(hash, otp.CodeHash, StringComparison.OrdinalIgnoreCase))
            {
                otp.AttemptCount++;
                otpRepo.Update(otp);
                await _unitOfWork.CompleteAsync(ct);
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    ["code"] = ["Invalid verification code."]
                });
            }

            otp.IsUsed = true;
            otpRepo.Update(otp);
            await _unitOfWork.CompleteAsync(ct);
        }

        private static string BuildOtpEmailBody(string displayName, string code, OtpPurpose purpose)
        {
            var action = purpose switch
            {
                OtpPurpose.Registration => "complete your Connectly registration",
                OtpPurpose.ForgotPassword => "reset your Connectly password",
                OtpPurpose.EmailChange => "confirm your new email address on Connectly",
                _ => "verify your request on Connectly"
            };

            return $"""
                <p>Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
                <p>Use this verification code to {action}:</p>
                <p style="font-size:1.75rem;font-weight:700;letter-spacing:0.35rem;margin:1rem 0">{code}</p>
                <p>This code expires in 10 minutes. If you did not request this, you can ignore this email.</p>
                """;
        }
    }
}
