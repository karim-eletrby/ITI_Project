using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class FeedController : Controller
{
    private readonly IPostService _postService;
    private readonly IFriendshipService _friendshipService;

    public FeedController(IPostService postService, IFriendshipService friendshipService)
    {
        _postService = postService;
        _friendshipService = friendshipService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1, CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            TempData["ErrorMessage"] = "Your session is invalid. Please sign in again.";
            return View("~/Views/Shared/Error.cshtml");
        }

        var result = await _postService.GetFeedAsync(currentUserId, Math.Max(page, 1), pageSize: 10, ct: ct);
        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] = result.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        var birthdays = await _friendshipService.GetFriendsBirthdaysTodayAsync(currentUserId, ct);
        ViewBag.BirthdaysToday = birthdays.Data ?? [];

        return View(result.Data);
    }
}
