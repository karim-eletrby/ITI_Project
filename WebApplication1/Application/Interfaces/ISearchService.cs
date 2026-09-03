using Application.Common;
using Application.DTOs.SearchDtos;

namespace Application.Interfaces;

public interface ISearchService
{
    Task<Result<SearchResultDto>> SearchAsync(string currentUserId, string query, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UserSearchResultDto>>> GetDiscoverUsersAsync(string currentUserId, int page = 1, int pageSize = 20, string? query = null, CancellationToken ct = default);
}
