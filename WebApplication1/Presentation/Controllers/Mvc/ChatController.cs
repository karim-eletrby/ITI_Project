using System.Security.Claims;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models.ViewModels;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class ChatController : Controller
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? id, CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            TempData["ErrorMessage"] = "Your session is invalid. Please sign in again.";
            return View("~/Views/Shared/Error.cshtml");
        }

        var conversationsResult = await _chatService.GetConversationsSummaryAsync(currentUserId, ct);
        if (!conversationsResult.IsSuccess || conversationsResult.Data is null)
        {
            TempData["ErrorMessage"] = conversationsResult.Message;
            return View("~/Views/Shared/Error.cshtml");
        }

        var model = new ChatPageViewModel
        {
            Conversations = conversationsResult.Data,
            CurrentUserId = currentUserId,
            ActiveUserId = id
        };

        if (!string.IsNullOrWhiteSpace(id))
        {
            var contextResult = await _chatService.GetConversationContextAsync(currentUserId, id, ct);
            if (contextResult.IsSuccess && contextResult.Data is not null)
            {
                var ctx = contextResult.Data;
                model.ActiveUserName = ctx.OtherUserDisplayName;
                model.ActiveIsRequest = ctx.IsRequestConversation;
                model.IsIncomingMessageRequest = ctx.IsIncomingRequest;
                model.IsOutgoingMessageRequest = ctx.IsOutgoingRequest;
                model.CanSendMessage = ctx.CanSendMessage;
                model.SendBlockedReason = ctx.SendBlockedReason;
                model.RelationshipStatus = ctx.RelationshipStatus;
            }

            var messagesResult = await _chatService.GetConversationAsync(currentUserId, id, ct);
            if (messagesResult.IsSuccess && messagesResult.Data is not null)
                model.Messages = messagesResult.Data;

            await _chatService.MarkAsReadAsync(currentUserId, id, ct);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Conversation(string id) => RedirectToAction(nameof(Index), new { id });
}
