using Application.DTOs.PostsDtos;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    
    public class PostsController : ApiController
    {
        private readonly IPostService _postService;
        private readonly IFileStorageService _fileStorage;

        public PostsController(IPostService postService, IFileStorageService fileStorage)
        {
            _postService = postService;
            _fileStorage = fileStorage;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto dto, CancellationToken ct)
        {
            var result = await _postService.CreatePostAsync(CurrentUserId, dto, ct);
            return CreatedAtAction(nameof(GetPostById), new { id = result.Data!.Id }, result.ToSuccessResponse());
        }

        [HttpPost("upload")]
        [RequestSizeLimit(1_048_576_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 1_048_576_000)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreatePostWithMedia(
            [FromForm] IFormFile? media,
            [FromForm] string? content,
            [FromForm] PostPrivacy privacy = PostPrivacy.Public,
            CancellationToken ct = default)
        {
            try
            {
                string? mediaUrl = null;

                if (media is not null && media.Length > 0)
                {
                    await using var stream = media.OpenReadStream();
                    mediaUrl = await _fileStorage.SaveAsync(stream, media.FileName, StoredMediaKind.PostMedia, media.ContentType, ct);
                }

                if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(mediaUrl))
                    return BadRequest(new { message = "Add some text or attach a photo/video." });

                var dto = new CreatePostDto(content ?? string.Empty, mediaUrl, privacy);
                var result = await _postService.CreatePostAsync(CurrentUserId, dto, ct);
                return Ok(result.ToSuccessResponse());
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (AppException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, errors = ex.Errors });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPostById(int id, CancellationToken ct)
        {
            var result = await _postService.GetPostByIdAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var result = await _postService.GetFeedAsync(CurrentUserId, pageNumber, pageSize, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePost(int id, CancellationToken ct)
        {
            var result = await _postService.DeletePostAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("{id:int}/like")]
        public async Task<IActionResult> ToggleLike(int id, CancellationToken ct)
        {
            var result = await _postService.ToggleLikeAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("{id:int}/comments")]
        public async Task<IActionResult> GetComments(int id, CancellationToken ct)
        {
            var result = await _postService.GetPostCommentsAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("{id:int}/likes")]
        public async Task<IActionResult> GetLikes(int id, CancellationToken ct)
        {
            var result = await _postService.GetPostLikesAsync(id, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("{id:int}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] CreateCommentDto dto, CancellationToken ct)
        {
            var result = await _postService.AddCommentAsync(id, CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpDelete("{id:int}/comments/{commentId:int}")]
        public async Task<IActionResult> DeleteComment(int id, int commentId, CancellationToken ct)
        {
            var result = await _postService.DeleteCommentAsync(id, commentId, CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("{id:int}/share/feed")]
        public async Task<IActionResult> ShareToFeed(int id, [FromBody] SharePostToFeedDto dto, CancellationToken ct)
        {
            var result = await _postService.SharePostToFeedAsync(id, CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("{id:int}/share/chat")]
        public async Task<IActionResult> ShareToChat(int id, [FromBody] SharePostToChatDto dto, CancellationToken ct)
        {
            var result = await _postService.SharePostToChatAsync(id, CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }
    }
}
