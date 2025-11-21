using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Domain.Entities;
using BinomoBackend.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BinomoBackend.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var now = DateTime.UtcNow; // Важно так сделать много библиотек нужно так!!!!
        var expires = now.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        if (!string.IsNullOrEmpty(user.WalletAddress))
            claims.Add(new Claim("wallet_address", user.WalletAddress));

        if (user.WalletType.HasValue)
            claims.Add(new Claim("wallet_type", user.WalletType.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Console.WriteLine($"Generating access token ...");
        // Console.WriteLine(
        //     $"Subject: {new ClaimsIdentity(claims)}, Isser: {_jwtSettings.Issuer}, Audience: {_jwtSettings.Audience}, Expires: {expires}");
       
        // Очень важно чтобы библиотека была версия IdentityModel.Tokens.Jwt/7.5.2 для asp.net 8 https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        
        var decodedToken = tokenHandler.ReadJwtToken(tokenString);

        // DEBUG: Проверяем что сгенерировали
        // Console.WriteLine($"[JWT] ✅ Generated token for {user.Email}");
        // Console.WriteLine($"[JWT] Issuer: '{decodedToken.Issuer}'");
        // Console.WriteLine($"[JWT] Audience: '{string.Join(", ", decodedToken.Audiences)}'");
        // Console.WriteLine($"[JWT] Expires (ValidTo): {decodedToken.ValidTo:yyyy-MM-dd HH:mm:ss} UTC");
        // Console.WriteLine($"[JWT] Valid From: {decodedToken.ValidFrom:yyyy-MM-dd HH:mm:ss} UTC");

        Console.WriteLine($"[JWT] Payload claims:");
        if (decodedToken.Payload.TryGetValue("exp", out var expValue))
            Console.WriteLine($"  ✅ exp: {expValue}");
        else
            Console.WriteLine($"  ❌ exp: MISSING!");

        if (decodedToken.Payload.TryGetValue("nbf", out var nbfValue))
            Console.WriteLine($"  ✅ nbf: {nbfValue}");

        if (decodedToken.Payload.TryGetValue("iat", out var iatValue))
            Console.WriteLine($"  ✅ iat: {iatValue}");

        if (decodedToken.Payload.TryGetValue("iss", out var issValue))
            Console.WriteLine($"  ✅ iss: '{issValue}'");

        if (decodedToken.Payload.TryGetValue("aud", out var audValue))
            Console.WriteLine($"  ✅ aud: '{audValue}'");

        return tokenString;
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public DateTime GetRefreshTokenExpiration()
    {
        return DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
    }
}