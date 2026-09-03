using Application.Interfaces.Repositries;
using Application.Interfaces.unitofwork;
using Domain.Common;
using Infrastructure.Context;
using System.Collections.Concurrent;

namespace Application.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly ConcurrentDictionary<string, object> _repositories = new();

        public IPostRepository Posts { get; }
        public IFriendshipRepository Friendships { get; }
        public IRefreshTokenRepository RefreshTokens { get; }

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Posts = new PostRepository(_context);
            Friendships = new FriendshipRepository(_context);
            RefreshTokens = new RefreshTokenRepository(_context);
        }

        public IGenericRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>
        {
            var typeName = typeof(T).Name;
            return (IGenericRepository<T, TKey>)_repositories.GetOrAdd(
                typeName,
                _ => new GenericRepository<T, TKey>(_context)
            );
        }

        public async Task<int> CompleteAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);

        public void Dispose() => _context.Dispose();
    }
}
