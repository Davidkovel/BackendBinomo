using System.Security.Claims;
using System.Text;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Infrastructure.Configuration;
using BinomoBackend.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BinomoBackend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("JwtSettings");
        services.Configure<JwtSettings>(jwtSection);
        var jwtSettings = jwtSection.Get<JwtSettings>();

        if (jwtSettings == null)
            throw new InvalidOperationException("JwtSettings section is missing in appsettings.json");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
            throw new InvalidOperationException("JWT Secret is not configured");

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
            throw new InvalidOperationException("JWT Issuer is not configured");

        if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
            throw new InvalidOperationException("JWT Audience is not configured");

        // Console.WriteLine("=== JWT CONFIGURATION ===");
        // Console.WriteLine($"Issuer: {jwtSettings.Issuer}");
        // Console.WriteLine($"Audience: {jwtSettings.Audience}");
        // Console.WriteLine($"Secret Length: {jwtSettings.Secret.Length}");
        // Console.WriteLine($"Access Token Expiration: {jwtSettings.AccessTokenExpirationMinutes} minutes");
        // Console.WriteLine($"Refresh Token Expiration: {jwtSettings.RefreshTokenExpirationDays} days");
        // Console.WriteLine("========================");

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IWalletSignatureValidator, WalletSignatureValidator>();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireExpirationTime = true
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[JWT] Authentication failed: {context.Exception.Message}");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        Console.WriteLine($"[JWT] Token validated successfully for user: {userId}");
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}