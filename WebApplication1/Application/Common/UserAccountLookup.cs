using Domain.Entites;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Common;

public static class UserAccountLookup
{
    public static string SanitizeLogin(string login)
    {
        var trimmed = login.Trim();
        while (trimmed.StartsWith('@'))
            trimmed = trimmed[1..].TrimStart();
        return trimmed;
    }

    public static async Task<ApplicationUser?> FindByLoginAsync(
        UserManager<ApplicationUser> userManager,
        string login,
        CancellationToken ct = default)
    {
        var trimmed = SanitizeLogin(login);
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (trimmed.Contains('@'))
        {
            var byEmail = await userManager.FindByEmailAsync(trimmed);
            if (byEmail != null)
                return byEmail;
        }

        string username;
        try
        {
            username = UsernameValidator.Normalize(trimmed);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var byName = await userManager.FindByNameAsync(username);
        if (byName != null)
            return byName;

        var normalized = userManager.NormalizeName(username);
        return await userManager.Users.FirstOrDefaultAsync(
            u => u.NormalizedUserName == normalized || u.UserName == username,
            ct);
    }
}
