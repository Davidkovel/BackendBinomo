namespace BinomoBackend.Infrastructure.Configuration;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "BinomoBackend";
    public string Audience { get; set; } = "BinomoBackend";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}