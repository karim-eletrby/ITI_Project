using Domain.Enums;

namespace Application.Interfaces
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(
            string recipientId,
            string? triggeredById,
            NotificationType type,
            string message,
            string? targetUrl,
            CancellationToken ct = default);
    }
}
