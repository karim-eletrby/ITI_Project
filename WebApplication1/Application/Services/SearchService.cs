using Application.Common;
using Application.DTOs.SearchDtos;
using Application.Interfaces;
using Application.Interfaces.unitofwork;
using Domain.Enums;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class SearchService : ISearchService
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFriendshipService _friendshipService;

    public SearchService(ApplicationDbContext context, IUnitOfWork unitOfWork, IFriendshipService friendshipService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _friendshipService = friendshipService;
    }

    public async Task<Result<SearchResultDto>> SearchAsync(string currentUserId, string query, CancellationToken ct = default)
    {
        var term = query.Trim();
        if (term.Length < 1)
            return Result<SearchResultDto>.Success(new SearchResultDto([]), "Enter a name or keyword to search.");

        var pattern = $"%{term}%";
        var blockedIds = await GetBlockedUserIdsAsync(currentUserId, ct);

        var rawUsers = await _context.Users.AsNoTracking()
            .Where(u => u.Id != currentUserId &&
                        !blockedIds.Contains(u.Id) &&
                        (EF.Functions.Like(u.DisplayName, pattern) ||
                         EF.Functions.Like(u.UserName!, pattern) ||
                         (u.Bio != null && EF.Functions.Like(u.Bio, pattern))))
            .OrderBy(u => u.DisplayName)
            .Take(15)
            .Select(u => new { u.Id, u.DisplayName, u.UserName, u.Bio, u.ProfilePictureUrl })
            .ToListAsync(ct);

        var users = new List<UserSearchResultDto>();
        foreach (var user in rawUsers)
        {
            var status = await _friendshipService.GetRelationshipStatusAsync(currentUserId, user.Id, ct);
            users.Add(new UserSearchResultDto(
                user.Id,
                user.DisplayName,
                user.UserName ?? user.DisplayName,
                user.Bio,
                user.ProfilePictureUrl,
                status));
        }

        return Result<SearchResultDto>.Success(new SearchResultDto(users));
    }

    public async Task<Result<IReadOnlyList<UserSearchResultDto>>> GetDiscoverUsersAsync(
        string currentUserId,
        int page = 1,
        int pageSize = 20,
        string? query = null,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 5, 50);

        var blockedIds = await GetBlockedUserIdsAsync(currentUserId, ct);
        var excludeIds = blockedIds.Append(currentUserId).ToHashSet();

        var term = query?.Trim();
        var isSearch = !string.IsNullOrWhiteSpace(term);

        if (!isSearch)
        {
            var friendIds = await _unitOfWork.Friendships.GetAcceptedFriendIdsAsync(currentUserId, ct);
            foreach (var friendId in friendIds)
                excludeIds.Add(friendId);
        }

        var usersQuery = _context.Users.AsNoTracking()
            .Where(u => !excludeIds.Contains(u.Id));

        if (isSearch)
        {
            var pattern = $"%{term}%";
            usersQuery = usersQuery.Where(u =>
                EF.Functions.Like(u.DisplayName, pattern) ||
                EF.Functions.Like(u.UserName!, pattern) ||
                (u.Bio != null && EF.Functions.Like(u.Bio, pattern)));
            usersQuery = usersQuery.OrderBy(u => u.DisplayName);
        }
        else
        {
            usersQuery = usersQuery.OrderByDescending(u => u.CreatedAt);
        }

        var rawUsers = await usersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.DisplayName, u.UserName, u.Bio, u.ProfilePictureUrl })
            .ToListAsync(ct);

        var users = new List<UserSearchResultDto>();
        foreach (var user in rawUsers)
        {
            var status = await _friendshipService.GetRelationshipStatusAsync(currentUserId, user.Id, ct);
            users.Add(new UserSearchResultDto(
                user.Id,
                user.DisplayName,
                user.UserName ?? user.DisplayName,
                user.Bio,
                user.ProfilePictureUrl,
                status));
        }

        return Result<IReadOnlyList<UserSearchResultDto>>.Success(users);
    }

    private async Task<List<string>> GetBlockedUserIdsAsync(string currentUserId, CancellationToken ct)
    {
        return await _context.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendShipStatus.Blocked &&
                        (f.RequesterId == currentUserId || f.ReceiverId == currentUserId))
            .Select(f => f.RequesterId == currentUserId ? f.ReceiverId : f.RequesterId)
            .ToListAsync(ct);
    }
}
