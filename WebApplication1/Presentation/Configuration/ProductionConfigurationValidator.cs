namespace Presentation.Configuration;

public static class ProductionConfigurationValidator
{
    private const string DevelopmentJwtKey = "hXjuMm1rm31dvDNo50VsSNLFvMZnJYn3ylR4ptbkW7M";
    private const string DevelopmentOtpPepper = "hXjuMm1rm31dvDNo50VsSNLFvMZnJYn3ylR4ptbkW7M";

    public static void Validate(IConfiguration configuration)
    {
        var errors = new List<string>();

        var connectionString = configuration.GetConnectionString("Conn");
        if (string.IsNullOrWhiteSpace(connectionString))
            errors.Add("ConnectionStrings:Conn must be configured for production.");

        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
            errors.Add("Jwt:Key must be at least 32 characters in production.");
        else if (jwtKey == DevelopmentJwtKey)
            errors.Add("Jwt:Key must not use the development default in production.");

        var otpPepper = configuration["Otp:Pepper"];
        if (string.IsNullOrWhiteSpace(otpPepper) || otpPepper.Length < 32)
            errors.Add("Otp:Pepper must be at least 32 characters in production.");
        else if (otpPepper == DevelopmentOtpPepper)
            errors.Add("Otp:Pepper must not use the development default in production.");

        var publicUrl = configuration["App:PublicUrl"];
        if (string.IsNullOrWhiteSpace(publicUrl) || publicUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            errors.Add("App:PublicUrl must be set to your public HTTPS URL in production.");

        var smtpEmail = configuration["Smtp:SenderEmail"];
        var smtpPassword = configuration["Smtp:SenderPassword"];
        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
            errors.Add("Smtp:Host must be configured in production.");
        if (string.IsNullOrWhiteSpace(smtpEmail))
            errors.Add("Smtp:SenderEmail must be configured in production.");
        if (string.IsNullOrWhiteSpace(smtpPassword))
            errors.Add("Smtp:SenderPassword must be configured in production.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Production configuration is invalid:\n- " + string.Join("\n- ", errors));
    }
}
