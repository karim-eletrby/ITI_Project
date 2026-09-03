using Application.DTOs.SearchDtos;

namespace WebApplication1.Models.ViewModels;

public class SearchPageViewModel
{
    public string Query { get; set; } = string.Empty;
    public SearchResultDto Results { get; set; } = new([]);
    public IReadOnlyList<UserSearchResultDto> DiscoverUsers { get; set; } = [];
}
