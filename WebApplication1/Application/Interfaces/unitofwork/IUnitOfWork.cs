using Application.Interfaces.Repositries;
using Domain.Common;

namespace Application.Interfaces.unitofwork
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>;
        IPostRepository Posts { get; }
        IFriendshipRepository Friendships { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        Task<int> CompleteAsync(CancellationToken cancellationToken = default);
    }
}
