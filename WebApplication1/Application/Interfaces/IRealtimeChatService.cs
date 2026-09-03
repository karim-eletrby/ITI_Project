using Application.DTOs.MessageDtos;

namespace Application.Interfaces
{
    public interface IRealtimeChatService
    {
        Task PushMessageToUserAsync(string userId, MessageDto message, CancellationToken ct = default);
    }
}
