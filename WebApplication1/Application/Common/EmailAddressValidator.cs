using System.ComponentModel.DataAnnotations;
using Application.Exceptions;

namespace Application.Common
{
    public static class EmailAddressValidator
    {
        private static readonly EmailAddressAttribute EmailValidator = new();

        public static string Normalize(string email) => email.Trim().ToLowerInvariant();

        public static bool IsValidFormat(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var trimmed = email.Trim();
            if (trimmed.Length > 256)
                return false;

            return EmailValidator.IsValid(trimmed);
        }

        public static string NormalizeOrThrow(string email, string fieldName = "newEmail")
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    [fieldName] = ["Email is required."]
                });
            }

            var trimmed = email.Trim();
            if (!IsValidFormat(trimmed))
            {
                throw new BadRequestException("Please fix the errors below.", new Dictionary<string, string[]>
                {
                    [fieldName] = ["Enter a valid email address."]
                });
            }

            return Normalize(trimmed);
        }
    }
}
