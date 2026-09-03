using Application.DTOs.PostsDtos;

namespace WebApplication1.Models.ViewModels;

public class PostsIndexViewModel
{
    public CreatePostDto CreatePost { get; set; } = new(string.Empty, null);
    public IReadOnlyList<PostDto> Posts { get; set; } = Array.Empty<PostDto>();
}
