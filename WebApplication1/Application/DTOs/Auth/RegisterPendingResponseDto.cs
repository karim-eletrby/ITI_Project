namespace Application.DTOs.Auth
{
    public record RegisterPendingResponseDto(
        string UserId,
        string Email,
        bool EmailSent
    );
}
