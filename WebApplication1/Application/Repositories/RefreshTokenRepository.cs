using Application.Interfaces.Repositries;
using Domain.Entites;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;


namespace Application.Repositories
{
    public class RefreshTokenRepository : GenericRepository<RefreshToken, int>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        {
            return await _dbSet
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == token, ct);
        }

        public async Task<RefreshToken?> GetActiveTokenByUserAsync(string userId, string token, CancellationToken ct = default)
        {
            return await _dbSet
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Token == token && r.RevokedOn == null && r.ExpiresOn > DateTime.UtcNow, ct);
        }
    }
}
