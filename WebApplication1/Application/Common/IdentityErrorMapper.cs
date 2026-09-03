using Microsoft.AspNetCore.Identity;

namespace Application.Common;

public static class IdentityErrorMapper
{
    public static Dictionary<string, string[]> MapRegistrationErrors(IEnumerable<IdentityError> errors)
    {
        var fieldErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in errors)
        {
            var field = error.Code switch
            {
                "DuplicateUserName" => "username",
                "InvalidUserName" => "username",
                "DuplicateEmail" => "email",
                "InvalidEmail" => "email",
                "PasswordTooShort" => "password",
                "PasswordRequiresNonAlphanumeric" => "password",
                "PasswordRequiresDigit" => "password",
                "PasswordRequiresLower" => "password",
                "PasswordRequiresUpper" => "password",
                _ => "general"
            };

            if (!fieldErrors.TryGetValue(field, out var list))
            {
                list = [];
                fieldErrors[field] = list;
            }

            list.Add(error.Description);
        }

        return fieldErrors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }
}
