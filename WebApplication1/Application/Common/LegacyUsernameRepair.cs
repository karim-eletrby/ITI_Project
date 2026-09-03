using System.Text.RegularExpressions;
using Domain.Entites;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Common;

public static partial class LegacyUsernameRepair
{
    [GeneratedRegex("[^a-z0-9._]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidUsernameChars();

    public static async Task RunAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken ct = default)
    {
        var users = await userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(' '))
            .ToListAsync(ct);

        foreach (var user in users)
        {
            var username = await AllocateUsernameAsync(userManager, user, ct);
            if (username is null)
                continue;

            await userManager.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(u => u.UserName, username)
                        .SetProperty(u => u.NormalizedUserName, userManager.NormalizeName(username)),
                    ct);

            logger.LogInformation(
                "Repaired legacy username for user {UserId}: {Username}",
                user.Id,
                username);
        }
    }

    private static async Task<string?> AllocateUsernameAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        CancellationToken ct)
    {
        var candidates = BuildCandidates(user);
        foreach (var candidate in candidates)
        {
            if (!UsernameValidator.IsValid(candidate))
                continue;

            var taken = await userManager.FindByNameAsync(candidate);
            if (taken == null || taken.Id == user.Id)
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> BuildCandidates(ApplicationUser user)
    {
        var source = user.DisplayName;
        if (string.IsNullOrWhiteSpace(source))
            source = user.Email?.Split('@')[0] ?? user.Id;

        var normalized = source.Trim().ToLowerInvariant().Replace(' ', '.');
        normalized = InvalidUsernameChars().Replace(normalized, string.Empty);
        normalized = normalized.Trim('.');

        if (normalized.Length >= UsernameValidator.MinLength)
            yield return normalized.Length <= UsernameValidator.MaxLength
                ? normalized
                : normalized[..UsernameValidator.MaxLength];

        var compact = normalized.Replace(".", string.Empty);
        if (compact.Length >= UsernameValidator.MinLength)
            yield return compact.Length <= UsernameValidator.MaxLength
                ? compact
                : compact[..UsernameValidator.MaxLength];

        var suffix = user.Id.Replace("-", string.Empty)[..6];
        var withSuffix = $"{normalized}.{suffix}".Trim('.');
        if (withSuffix.Length > UsernameValidator.MaxLength)
            withSuffix = withSuffix[..UsernameValidator.MaxLength];

        if (withSuffix.Length >= UsernameValidator.MinLength)
            yield return withSuffix;
    }
}
