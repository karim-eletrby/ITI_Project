using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models.ViewModels;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class FriendshipsController : Controller
{
    private readonly IFriendshipService _friendshipService;
    private readonly ISearchService _searchService;

    public FriendshipsController(IFriendshipService friendshipService, ISearchService searchService)
    {
        _friendshipService = friendshipService;
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Pending(CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            TempData["ErrorMessage"] = "Your session is invalid. Please sign in again.";
            return View("~/Views/Shared/Error.cshtml");
        }

        var historyResult = await _friendshipService.GetIncomingRequestHistoryAsync(currentUserId, ct);
        if (!historyResult.IsSuccess || historyResult.Data is null)
        {
            TempData["ErrorMessage"] = historyResult.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        var suggestedResult = await _searchService.GetDiscoverUsersAsync(currentUserId, 1, 12, ct: ct);
        var model = new FriendRequestsPageViewModel
        {
            IncomingRequests = historyResult.Data,
            PeopleYouMayKnow = suggestedResult.IsSuccess && suggestedResult.Data is not null
                ? suggestedResult.Data
                : Array.Empty<Application.DTOs.SearchDtos.UserSearchResultDto>()
        };

        return View(model);
    }
}
