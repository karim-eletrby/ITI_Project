using Application.Common;
using Domain.Entites;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.Repositries
{
    public interface IPostRepository : IGenericRepository<Post, int>
    {
        Task<Post?> GetPostWithDetailsAsync(int postId, CancellationToken cancellationToken = default);
        Task<PagedResult<Post>> GetFeedPostsAsync(string currentUserId, IEnumerable<string> friendIds, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<PagedResult<Post>> GetUserPostsAsync(string profileUserId, string viewerUserId, bool viewerIsFriend, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task ClearShareReferencesAsync(int postId, CancellationToken cancellationToken = default);
    }
}
