using Domain.Entites;

namespace Application.Interfaces.Repositries
{
    public interface IRefreshTokenRepository : IGenericRepository<RefreshToken, int>
    {
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
        Task<RefreshToken?> GetActiveTokenByUserAsync(string userId, string token, CancellationToken ct = default);
    }
}
