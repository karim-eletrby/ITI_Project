namespace Infrastructure.Configuration
{
    public class OtpOptions
    {
        public const string SectionName = "Otp";
        public int ExpiryMinutes { get; set; } = 10;
        public int MaxAttempts { get; set; } = 5;
        public int ResendCooldownSeconds { get; set; } = 60;
        public string Pepper { get; set; } = "connectly-otp-pepper";
    }
}
