using System.Text.RegularExpressions;

namespace Application.Common
{
    public static partial class UsernameValidator
    {
        public const int MinLength = 3;
        public const int MaxLength = 30;

        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "administrator", "connectly", "root", "system", "support", "help",
            "api", "www", "mail", "null", "undefined", "me", "you", "feed", "chat", "profile"
        };

        [GeneratedRegex("^[a-zA-Z0-9._]+$", RegexOptions.CultureInvariant)]
        private static partial Regex UsernamePattern();

        public static string Normalize(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.");

            return username.Trim().ToLowerInvariant();
        }

        public static void Validate(string normalizedUsername)
        {
            if (!IsValid(normalizedUsername))
                throw new ArgumentException(GetValidationMessage(normalizedUsername));
        }

        public static bool IsValid(string normalizedUsername)
        {
            if (normalizedUsername.Length < MinLength || normalizedUsername.Length > MaxLength)
                return false;

            if (!UsernamePattern().IsMatch(normalizedUsername))
                return false;

            if (normalizedUsername.StartsWith('.') || normalizedUsername.EndsWith('.') ||
                normalizedUsername.StartsWith('_') || normalizedUsername.EndsWith('_'))
                return false;

            if (normalizedUsername.Contains("..") || normalizedUsername.Contains("__"))
                return false;

            if (Reserved.Contains(normalizedUsername))
                return false;

            return true;
        }

        private static string GetValidationMessage(string normalizedUsername)
        {
            if (normalizedUsername.Length < MinLength || normalizedUsername.Length > MaxLength)
                return $"Username must be {MinLength}-{MaxLength} characters.";

            if (!UsernamePattern().IsMatch(normalizedUsername))
                return "Username can only contain letters, numbers, dots, and underscores.";

            if (normalizedUsername.StartsWith('.') || normalizedUsername.EndsWith('.') ||
                normalizedUsername.StartsWith('_') || normalizedUsername.EndsWith('_'))
                return "Username cannot start or end with a dot or underscore.";

            if (normalizedUsername.Contains("..") || normalizedUsername.Contains("__"))
                return "Username cannot contain consecutive dots or underscores.";

            if (Reserved.Contains(normalizedUsername))
                return "This username is reserved. Please choose another.";

            return "Invalid username.";
        }
    }
}
