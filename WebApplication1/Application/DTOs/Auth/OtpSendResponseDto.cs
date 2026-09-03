namespace Application.DTOs.Auth
{
    public record OtpSendResponseDto(
        bool EmailSent,
        string Message
    );
}
