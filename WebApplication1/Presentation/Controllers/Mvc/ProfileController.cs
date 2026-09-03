using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models.ViewModels;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class ProfileController : Controller
{
    private readonly IAuthService _authService;
    private readonly IFriendshipService _friendshipService;
    private readonly IPostService _postService;

    public ProfileController(IAuthService authService, IFriendshipService friendshipService, IPostService postService)
    {
        _authService = authService;
        _friendshipService = friendshipService;
        _postService = postService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? userId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            TempData["ErrorMessage"] = "Your session is invalid. Please sign in again.";
            return View("~/Views/Shared/Error.cshtml");
        }

        var profileResult = await _authService.GetProfileAsync(targetUserId, ct);
        var friendsResult = await _friendshipService.GetFriendsAsync(targetUserId, ct);
        if (!profileResult.IsSuccess || profileResult.Data is null || !friendsResult.IsSuccess || friendsResult.Data is null)
        {
            TempData["ErrorMessage"] = profileResult.IsSuccess ? friendsResult.Message : profileResult.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        var friendshipStatus = targetUserId == currentUserId
            ? "Self"
            : await _friendshipService.GetRelationshipStatusAsync(currentUserId!, targetUserId, ct);

        var postsResult = await _postService.GetUserPostsAsync(targetUserId, currentUserId!, Math.Max(page, 1), pageSize: 10, ct);
        if (!postsResult.IsSuccess || postsResult.Data is null)
        {
            TempData["ErrorMessage"] = postsResult.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        return View(new ProfilePageViewModel
        {
            Profile = profileResult.Data,
            Friends = friendsResult.Data,
            Posts = postsResult.Data,
            IsCurrentUser = targetUserId == currentUserId,
            FriendshipStatus = friendshipStatus
        });
    }

    [HttpGet]
    public async Task<IActionResult> Friends(string? userId, CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId;
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            TempData["ErrorMessage"] = "Your session is invalid. Please sign in again.";
            return View("~/Views/Shared/Error.cshtml");
        }

        var profileResult = await _authService.GetProfileAsync(targetUserId, ct);
        var friendsResult = await _friendshipService.GetFriendsAsync(targetUserId, ct);
        if (!profileResult.IsSuccess || profileResult.Data is null || !friendsResult.IsSuccess || friendsResult.Data is null)
        {
            TempData["ErrorMessage"] = profileResult.IsSuccess ? friendsResult.Message : profileResult.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        return View(new ProfileFriendsPageViewModel
        {
            Profile = profileResult.Data,
            Friends = friendsResult.Data,
            IsCurrentUser = targetUserId == currentUserId
        });
    }
}
