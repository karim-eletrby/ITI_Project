using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ApiController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(CancellationToken ct)
        {
            var result = await _notificationService.GetUserNotificationsAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
        {
            var result = await _notificationService.MarkNotificationAsReadAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
        {
            var result = await _notificationService.MarkAllAsReadAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }
    }
}
