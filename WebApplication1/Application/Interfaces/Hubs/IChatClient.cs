using Application.DTOs.MessageDtos;


namespace Application.Interfaces.Hubs
{
    public interface IChatClient
    {
        Task ReceiveMessage(MessageDto message);
        Task MessageRead(int messageId, string readerId);
        Task UserTyping(string senderId);
        Task UserStoppedTyping(string senderId);
    }
}
