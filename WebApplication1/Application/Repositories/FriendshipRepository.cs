using Application.Interfaces.Repositries;
using Domain.Entites;
using Domain.Enums;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Repositories
{
    public class FriendshipRepository : IFriendshipRepository
    {
        private readonly ApplicationDbContext _context;

        public FriendshipRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Friendship?> GetFriendshipAsync(string userId1, string userId2, CancellationToken ct = default)
        {
            return await _context.Friendships
                .Include(f => f.Requester)
                .Include(f => f.Receiver)
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == userId1 && f.ReceiverId == userId2) ||
                    (f.RequesterId == userId2 && f.ReceiverId == userId1), ct);
        }

        public async Task<IReadOnlyList<Friendship>> GetUserFriendshipsByStatusAsync(string userId, FriendShipStatus status, CancellationToken ct = default)
        {
            return await _context.Friendships
                .Include(f => f.Requester)
                .Include(f => f.Receiver)
                .Where(f => (f.RequesterId == userId || f.ReceiverId == userId) && f.Status == status)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Friendship>> GetIncomingFriendRequestHistoryAsync(string receiverId, CancellationToken ct = default)
        {
            return await _context.Friendships
                .Include(f => f.Requester)
                .Include(f => f.Receiver)
                .Where(f => f.ReceiverId == receiverId &&
                    (f.Status == FriendShipStatus.Pending ||
                     f.Status == FriendShipStatus.Accepted ||
                     f.Status == FriendShipStatus.Rejected))
                .OrderByDescending(f => f.Status == FriendShipStatus.Pending)
                .ThenByDescending(f => f.UpdatedAt ?? f.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<string>> GetAcceptedFriendIdsAsync(string userId, CancellationToken ct = default)
        {
            return await _context.Friendships
                .Where(f => f.Status == FriendShipStatus.Accepted && (f.RequesterId == userId || f.ReceiverId == userId))
                .Select(f => f.RequesterId == userId ? f.ReceiverId : f.RequesterId)
                .ToListAsync(ct);
        }

        public async Task AddAsync(Friendship friendship, CancellationToken ct = default)
            => await _context.Friendships.AddAsync(friendship, ct);

        public void Update(Friendship friendship) => _context.Friendships.Update(friendship);

        public void Delete(Friendship friendship) => _context.Friendships.Remove(friendship);
    }
}
