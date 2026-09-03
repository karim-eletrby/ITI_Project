namespace Infrastructure.Configuration;

public class AppOptions
{
    public const string SectionName = "App";
    public string PublicUrl { get; set; } = "https://localhost:7242";
}
