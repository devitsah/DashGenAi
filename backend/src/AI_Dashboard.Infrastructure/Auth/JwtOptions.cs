namespace AI_Dashboard.Infrastructure.Auth;

public class JwtOptions
{
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "AI_Dashboard";
    public string Audience { get; set; } = "AI_Dashboard.Client";
    public int ExpiryMinutes { get; set; } = 120;
}