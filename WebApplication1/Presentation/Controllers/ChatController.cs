using Application.DTOs.MessageDtos;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ApiController
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, CancellationToken ct)
        {
            var result = await _chatService.SendMessageAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversationsSummary(CancellationToken ct)
        {
            var result = await _chatService.GetConversationsSummaryAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("messages/{otherUserId}/context")]
        public async Task<IActionResult> GetConversationContext(string otherUserId, CancellationToken ct)
        {
            var result = await _chatService.GetConversationContextAsync(CurrentUserId, otherUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("messages/{otherUserId}")]
        public async Task<IActionResult> GetConversation(string otherUserId, CancellationToken ct)
        {
            var result = await _chatService.GetConversationAsync(CurrentUserId, otherUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPut("messages/{senderId}/read")]
        public async Task<IActionResult> MarkMessagesAsRead(string senderId, CancellationToken ct)
        {
            var result = await _chatService.MarkAsReadAsync(CurrentUserId, senderId, ct);
            return Ok(result.ToSuccessResponse());
        }
    }
}
