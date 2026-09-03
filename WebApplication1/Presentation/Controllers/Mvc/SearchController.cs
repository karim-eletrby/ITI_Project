using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models.ViewModels;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return RedirectToAction("Login", "Auth");

        var query = q?.Trim() ?? string.Empty;
        var model = new SearchPageViewModel { Query = query };

        if (string.IsNullOrWhiteSpace(query))
        {
            var discover = await _searchService.GetDiscoverUsersAsync(currentUserId, ct: ct);
            model.DiscoverUsers = discover.Data ?? [];
            return View(model);
        }

        var result = await _searchService.SearchAsync(currentUserId, query, ct);
        model.Results = result.Data ?? new Application.DTOs.SearchDtos.SearchResultDto([]);
        return View(model);
    }
}
