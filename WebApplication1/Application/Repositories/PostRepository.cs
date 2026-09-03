using Application.Common;
using Application.Interfaces.Repositries;
using Domain.Entites;
using Domain.Enums;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Repositories
{
    public class PostRepository : GenericRepository<Post, int>, IPostRepository
    {
        public PostRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Post?> GetPostWithDetailsAsync(int postId, CancellationToken ct = default)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(p => p.User)
                .Include(p => p.SharedPost)
                    .ThenInclude(sp => sp!.User)
                .Include(p => p.Likes)
                    .ThenInclude(l => l.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.Id == postId, ct);
        }

        public async Task<PagedResult<Post>> GetFeedPostsAsync(
            string currentUserId,
            IEnumerable<string> friendIds,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var friendList = friendIds.ToList();

            var query = _dbSet.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.User)
                .Include(p => p.SharedPost)
                    .ThenInclude(sp => sp!.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Where(p =>
                    p.UserId == currentUserId ||
                    p.Privacy == PostPrivacy.Public ||
                    (p.Privacy == PostPrivacy.FriendsOnly && friendList.Contains(p.UserId)))
                .OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<Post>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<Post>> GetUserPostsAsync(
            string profileUserId,
            string viewerUserId,
            bool viewerIsFriend,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.User)
                .Include(p => p.SharedPost)
                    .ThenInclude(sp => sp!.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Where(p => p.UserId == profileUserId);

            if (profileUserId != viewerUserId)
            {
                query = query.Where(p =>
                    p.Privacy == PostPrivacy.Public ||
                    (p.Privacy == PostPrivacy.FriendsOnly && viewerIsFriend));
            }

            query = query.OrderByDescending(p => p.CreatedAt);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<Post>(items, totalCount, pageNumber, pageSize);
        }

        public async Task ClearShareReferencesAsync(int postId, CancellationToken ct = default)
        {
            await _dbSet
                .Where(p => p.SharedPostId == postId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SharedPostId, (int?)null), ct);
        }
    }
}
