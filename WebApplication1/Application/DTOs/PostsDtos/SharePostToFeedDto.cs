using Domain.Enums;

namespace Application.DTOs.PostsDtos
{
    public record SharePostToFeedDto(string? Caption, PostPrivacy Privacy = PostPrivacy.Public);
}
