using Domain.Entites;
using Domain.Enums;


namespace Application.Interfaces.Repositries
{
    public interface IFriendshipRepository
    {
        Task<Friendship?> GetFriendshipAsync(string userId1, string userId2, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Friendship>> GetUserFriendshipsByStatusAsync(string userId, FriendShipStatus status, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Friendship>> GetIncomingFriendRequestHistoryAsync(string receiverId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetAcceptedFriendIdsAsync(string userId, CancellationToken cancellationToken = default);
        Task AddAsync(Friendship friendship, CancellationToken cancellationToken = default);
        void Update(Friendship friendship);
        void Delete(Friendship friendship);
    }
}
