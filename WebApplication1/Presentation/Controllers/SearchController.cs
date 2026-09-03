using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ApiController
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _searchService.SearchAsync(CurrentUserId, q ?? string.Empty, ct);
        return Ok(result.ToSuccessResponse());
    }

    [HttpGet("discover")]
    public async Task<IActionResult> Discover([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null, CancellationToken ct = default)
    {
        var result = await _searchService.GetDiscoverUsersAsync(CurrentUserId, page, pageSize, q, ct);
        return Ok(result.ToSuccessResponse());
    }
}
