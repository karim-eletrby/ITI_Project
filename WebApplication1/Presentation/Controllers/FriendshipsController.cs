using Application.DTOs.FriendshipDtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FriendshipsController : ApiController
    {
        private readonly IFriendshipService _friendshipService;

        public FriendshipsController(IFriendshipService friendshipService)
        {
            _friendshipService = friendshipService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto, CancellationToken ct)
        {
            var result = await _friendshipService.SendRequestAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("respond")]
        public async Task<IActionResult> RespondToFriendRequest([FromBody] RespondFriendRequestDto dto, CancellationToken ct)
        {
            var result = await _friendshipService.RespondToRequestAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("friends/{userId}")]
        public async Task<IActionResult> GetUserFriends(string userId, CancellationToken ct)
        {
            var result = await _friendshipService.GetFriendsAsync(userId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("birthdays-today")]
        public async Task<IActionResult> GetFriendsBirthdaysToday(CancellationToken ct)
        {
            var result = await _friendshipService.GetFriendsBirthdaysTodayAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRequests(CancellationToken ct)
        {
            var result = await _friendshipService.GetPendingRequestsAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }
    }
}
