using System.Security.Cryptography;
using System.Text;
using Domain.Enums;

namespace Application.Common
{
    public static class OtpHasher
    {
        public static string Hash(string code, string email, OtpPurpose purpose, string? targetEmail, string pepper)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedTarget = targetEmail?.Trim().ToLowerInvariant() ?? string.Empty;
            var payload = $"{code.Trim()}:{normalizedEmail}:{normalizedTarget}:{(int)purpose}:{pepper}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }
    }
}
