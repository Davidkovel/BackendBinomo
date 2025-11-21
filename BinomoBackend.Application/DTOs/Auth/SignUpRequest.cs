using BinomoBackend.Domain.Entities;

namespace BinomoBackend.Application.DTOs.Auth;

public record SignUpRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record WalletAuthRequest(
    string WalletAddress,
    string Signature,
    string Message,
    WalletType WalletType,
    string Name,
    string Email
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    decimal Balance,
    string? WalletAddress,
    WalletType? WalletType
);